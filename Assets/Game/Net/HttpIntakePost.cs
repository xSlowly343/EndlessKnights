using System;
using System.Net.Http;
using System.Text;
using Game.Core.Intake;

namespace Game.Net
{
    public sealed class HttpIntakePost : IIntakePost
    {
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        public IntakeOutcome Post(string url, string json)
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = Client.PostAsync(url, content).GetAwaiter().GetResult();
            var status = (int)response.StatusCode;
            if (status >= 200 && status < 300)
                return IntakeOutcome.Delivered;
            if (status >= 400 && status < 500 && status != 408 && status != 429)
                return IntakeOutcome.Rejected;
            return IntakeOutcome.Failed;
        }
    }
}
