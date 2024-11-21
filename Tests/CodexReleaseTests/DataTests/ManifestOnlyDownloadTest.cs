﻿using CodexTests;
using NUnit.Framework;
using Utils;

namespace CodexReleaseTests.DataTests
{
    [TestFixture]
    public class ManifestOnlyDownloadTest : CodexDistTest
    {
        [Test]
        public void StreamlessTest()
        {
            var uploader = StartCodex();
            var downloader = StartCodex(s => s.WithBootstrapNode(uploader));

            var file = GenerateTestFile(2.GB());
            var size = Convert.ToInt64(file.GetFilesize());
            var cid = uploader.UploadFile(file);

            var startSpace = downloader.Space();
            var manifest = downloader.DownloadManifestOnly(cid);

            Thread.Sleep(1000);

            var spaceDiff = startSpace.FreeBytes - downloader.Space().FreeBytes;

            Assert.That(spaceDiff, Is.LessThan(64.KB().SizeInBytes));
            Assert.That(manifest.OriginalBytes.SizeInBytes, Is.EqualTo(file.GetFilesize().SizeInBytes));
        }
    }
}
