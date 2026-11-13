using Game.Core.Save;
using NUnit.Framework;

namespace Game.Core.Tests.Save
{
    public class CloudSyncTests
    {
        [Test]
        public void TheGameUploadsWhenItIsAheadAndOffersTheCloudWhenTheCloudIsAhead()
        {
            var local = new CloudMark(7200, 300, 10);
            Assert.That(CloudSync.Decide(local, null), Is.EqualTo(CloudAction.Upload), "nothing in the cloud yet");
            Assert.That(CloudSync.Decide(local, new CloudMark(7230, 300, 9)), Is.EqualTo(CloudAction.Same));
            Assert.That(CloudSync.Decide(local, new CloudMark(3600, 200, 5)), Is.EqualTo(CloudAction.Upload));
            Assert.That(CloudSync.Decide(local, new CloudMark(20000, 500, 5)), Is.EqualTo(CloudAction.OfferDownload), "played longer on another computer");
            Assert.That(CloudSync.Decide(local, new CloudMark(7200, 320, 5)), Is.EqualTo(CloudAction.OfferDownload), "same time, further record");
        }
    }
}
