using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Ui;
using Game.Net.Nakama;
using NUnit.Framework;

namespace Game.Net.Tests
{
    public class AccountSignInTests
    {
        private sealed class FakeServer : HttpMessageHandler
        {
            public readonly List<string> Paths = new List<string>();
            public Func<string, (HttpStatusCode, string)> Answer = _ => (HttpStatusCode.OK, "{}");

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var path = request.RequestUri!.PathAndQuery;
                Paths.Add(path);
                var (status, body) = Answer(path);
                return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
            }
        }

        private static string Token()
        {
            static string Part(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return Part("{\"alg\":\"HS256\"}") + "." + Part("{\"uid\":\"u-email\",\"exp\":4102444800}") + ".signature";
        }

        private static (NakamaClient, FakeServer) Client()
        {
            var server = new FakeServer();
            return (new NakamaClient("http://localhost:7350", "key", new HttpClient(server)), server);
        }

        [Test]
        public void ANewEmailIsLinkedToTheAccountOfThisComputer()
        {
            var (client, server) = Client();
            var session = NakamaSession.FromTokens(Token(), "");
            var (outcome, signed) = AccountSignIn.RunAsync(client, session, "ann@mail.ru", "secret123").GetAwaiter().GetResult();
            Assert.That(outcome, Is.EqualTo(AccountOutcome.SignedUp));
            Assert.That(signed, Is.SameAs(session));
            Assert.That(server.Paths, Is.EqualTo(new[] { "/v2/account/link/email" }));
        }

        [Test]
        public void AKnownEmailSignsIntoItsAccount()
        {
            var (client, server) = Client();
            server.Answer = path => path.StartsWith("/v2/account/link") ? (HttpStatusCode.Conflict, "{\"message\":\"Email is already in use.\"}")
                : (HttpStatusCode.OK, "{\"token\":\"" + Token() + "\",\"refresh_token\":\"\"}");
            var (outcome, signed) = AccountSignIn.RunAsync(client, NakamaSession.FromTokens(Token(), ""), "ann@mail.ru", "secret123").GetAwaiter().GetResult();
            Assert.That(outcome, Is.EqualTo(AccountOutcome.SignedIn));
            Assert.That(signed, Is.Not.Null);
            Assert.That(server.Paths[1], Is.EqualTo("/v2/account/authenticate/email?create=false"));
        }

        [Test]
        public void AWrongPasswordAndNoServerAreToldApart()
        {
            var (client, server) = Client();
            server.Answer = path => path.StartsWith("/v2/account/link") ? (HttpStatusCode.Conflict, "{}") : (HttpStatusCode.Unauthorized, "{\"message\":\"Invalid credentials.\"}");
            Assert.That(AccountSignIn.RunAsync(client, NakamaSession.FromTokens(Token(), ""), "ann@mail.ru", "wrongpass").GetAwaiter().GetResult().Outcome, Is.EqualTo(AccountOutcome.Wrong));
            server.Answer = _ => (HttpStatusCode.InternalServerError, "{}");
            Assert.That(AccountSignIn.RunAsync(client, null, "ann@mail.ru", "secret123").GetAwaiter().GetResult().Outcome, Is.EqualTo(AccountOutcome.Offline));
        }

        [Test]
        public void TheEntryChecksTheEmailAndPasswordAndNeverKeepsThePassword()
        {
            var entry = new AccountEntry();
            Assert.That(entry.Type('a'), Is.False, "nothing is focused");
            entry.FocusOn(AccountField.Email);
            entry.Paste(" Ann@Mail.ru ");
            Assert.That(entry.Email, Is.EqualTo("ann@mail.ru"));
            Assert.That(AccountSignIn.Problem(entry), Is.EqualTo("account.shortPassword"));
            entry.FocusOn(AccountField.Password);
            entry.Paste("secret123");
            Assert.That(entry.MaskedPassword, Is.EqualTo("•••••••••"));
            Assert.That(entry.Ready, Is.True);
            Assert.That(AccountSignIn.Problem(entry), Is.Empty);
            entry.SignedInAs(entry.Email);
            Assert.That(entry.Password, Is.Empty, "the password is forgotten after signing in");
            Assert.That(entry.SignedIn, Is.True);
            entry.FocusOn(AccountField.Email);
            for (var i = 0; i < 11; i++)
                entry.Type(CouponEntry.Backspace);
            entry.Paste("not-an-email");
            Assert.That(AccountSignIn.Problem(entry), Is.EqualTo("account.badEmail"));
        }
    }
}
