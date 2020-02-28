using System;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Logging;
using System.Security.Principal;
using System.Text;

namespace MCloudServer
{
    // "scoped" service
    // Every http request have it's own UserService instance.
    public class UserService
    {
        private readonly DbCtx dbctx;
        private HttpContext httpContext;

        public bool IsLogged => User != null;
        public User User { get; private set; }

        public LoginRecord LoginRecord { get; private set; }

        public UserService(DbCtx dbctx)
        {
            this.dbctx = dbctx;
            dbctx.UserService = this;
        }

        public async Task<string> TryLogin(string account, string password)
        {
            if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(password)) return null;

            var user = await dbctx.Users.Where(u =>
                    u.username == account
                ).SingleOrDefaultAsync();
            if (user == null) return null;
            if (DbCtx.ValidatePassword(password, user.passwd) == false) return null;

            LoginRecord record = await CreateLoginRecord(user);

            SetServiceState(record);

            return record.token;
        }

        public async Task Logout()
        {
            if (IsLogged && LoginRecord != null) {
                dbctx.Logins.Remove(LoginRecord);
                try {
                    await dbctx.SaveChangesAsync();
                } catch (DbUpdateConcurrencyException) {
                    // no-op: the record was already removed earlier
                }

                SetServiceState(null);
            }
        }

        public async Task Register(User user)
        {
            var username = user.username;
            if (await dbctx.Users.Where(u => u.username == username).AnyAsync())
                throw new UserServiceException("username_exists");

            dbctx.Users.Add(user);
            var record = CreateLoginRecord_NoSave(user);

            await dbctx.SaveChangesAsync();

            SetServiceState(record);
        }

        public Task CheckRequest()
        {
            var auth = httpContext.Request.Headers["Authorization"];
            if (auth.Count != 1) return Task.CompletedTask;
            return CheckToken(auth[0]);
        }

        public async Task CheckToken(string auth)
        {
            var r = await GetLoginFromToken(dbctx, auth);

            if (r.Record != null)
                SetServiceState(r.Record);
            else if (r.User != null)
                this.User = r.User;
        }

        public static async Task<GetLoginResult> GetLoginFromToken(DbCtx dbctx, string auth)
        {
            LoginRecord record = null;
            User user = null;

            var splits = auth.Split(' ');
            if (splits.Length != 2) return default;
            if (splits[0] == "Basic") {
                var kv = Encoding.UTF8.GetString(Convert.FromBase64String(splits[1])).Split(':');
                if (kv.Length < 2) return default;
                var username = kv[0];
                var passwd = kv[1];
                user = await dbctx.FindUser(username);
                if (user == null || !DbCtx.ValidatePassword(passwd, user.passwd)) user = null;
            } else if (splits[0] == "Bearer") {
                var token = splits[1];
                if (string.IsNullOrEmpty(token)) return default;
                record = await dbctx.FindLogin(token);
                if (record.last_used <= DateTime.Now.AddHours(-1)) {
                    record.last_used = DateTime.Now;
                    dbctx.Database.ExecuteSqlRaw("UPDATE logins SET last_used = {0} WHERE token = {1};",
                        record.last_used, record.token);
                }
                user = record.User;
            }
            return new GetLoginResult { Record = record, User = user };
        }

        public struct GetLoginResult
        {
            public LoginRecord Record;
            public User User;
        }

        private void SetServiceState(LoginRecord record)
        {
            this.LoginRecord = record;
            this.User = record?.User;

        }

        public async Task<LoginRecord> CreateLoginRecord(User user)
        {
            var record = CreateLoginRecord_NoSave(user);

            await dbctx.SaveChangesAsync();
            return record;
        }

        private LoginRecord CreateLoginRecord_NoSave(User user)
        {
            var tokenBytes = new byte[16];
            RandomNumberGenerator.Fill(tokenBytes);
            var token = Convert.ToBase64String(tokenBytes);
            var now = DateTime.Now;
            var record = new LoginRecord {
                token = token,
                User = user,
                login_date = now,
                last_used = now
            };
            dbctx.Logins.Add(record);
            return record;
        }

        public class AuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
        {
            private readonly UserService userService;

            public AuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
                    ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock, UserService userService)
                : base(options, logger, encoder, clock)
            {
                this.userService = userService;
            }

            protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
            {
                if (userService.httpContext != null)
                    throw new Exception("userService.httpContext != null");
                userService.httpContext = this.Request.HttpContext;
                await userService.CheckRequest();
                if (userService.IsLogged) {
                    var User = userService.User;
                    var identity = new GenericIdentity(User.username);
                    identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, User.username));
                    identity.AddClaim(new Claim(ClaimTypes.Role, User.role == UserRole.SuperAdmin ? "admin" : "user"));
                    var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
                    return AuthenticateResult.Success(ticket);
                } else {
                    return AuthenticateResult.NoResult();
                }
            }

            protected override Task HandleChallengeAsync(AuthenticationProperties properties)
            {
                Response.StatusCode = 403;
                return Task.CompletedTask;
            }
        }
    }

    public class UserServiceException : Exception
    {
        public UserServiceException(string message) : base(message)
        {
        }
    }
}