using System;
using NUnit.Framework;
using RikuK0j1ma.RaisingStackChan.Entity;
using RikuK0j1ma.RaisingStackChan.Infrastructure;

namespace RikuK0j1ma.RaisingStackChan.Tests.EditMode
{
    public sealed class StackChanLinkLogicTests
    {
        [Test]
        public void Endpoint_ParsesJsonPayload()
        {
            Assert.IsTrue(StackChanEndpoint.TryParse("{\"ip\":\"192.168.1.20\",\"port\":9000,\"token\":\"abc\"}", out var ep));
            Assert.AreEqual("192.168.1.20", ep.Host);
            Assert.AreEqual(9000, ep.Port);
            Assert.AreEqual("abc", ep.Token);
        }

        [Test]
        public void Endpoint_JsonWithoutPort_UsesDefault()
        {
            Assert.IsTrue(StackChanEndpoint.TryParse("{\"ip\":\"10.0.0.5\"}", out var ep));
            Assert.AreEqual(StackChanEndpoint.DefaultPort, ep.Port);
            Assert.AreEqual(string.Empty, ep.Token);
        }

        [Test]
        public void Endpoint_ParsesUrlPayload()
        {
            Assert.IsTrue(StackChanEndpoint.TryParse("ws://192.168.1.20:81/?token=a%20b", out var ep));
            Assert.AreEqual("192.168.1.20", ep.Host);
            Assert.AreEqual(81, ep.Port);
            Assert.AreEqual("a b", ep.Token);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("hello")]
        [TestCase("{broken")]
        [TestCase("{\"port\":80}")]
        [TestCase("{\"ip\":\"1.2.3.4\",\"port\":70000}")]
        public void Endpoint_RejectsInvalid(string payload)
        {
            Assert.IsFalse(StackChanEndpoint.TryParse(payload, out _));
        }

        [Test]
        public void Endpoint_SerializeRoundTrips()
        {
            var src = new StackChanEndpoint("192.168.1.20", 8080, "t&k");
            Assert.IsTrue(StackChanEndpoint.TryParse(src.Serialize(), out var back));
            Assert.AreEqual(src, back);
        }

        [Test]
        public void Endpoint_ToUri_IncludesEscapedToken()
        {
            var uri = new StackChanEndpoint("1.2.3.4", 81, "a b").ToUri();
            Assert.AreEqual("ws", uri.Scheme);
            Assert.AreEqual(81, uri.Port);
            StringAssert.Contains("token=a%20b", uri.Query);
            Assert.AreEqual("/ws", uri.AbsolutePath);
        }

        [Test]
        public void Endpoint_Mdns()
        {
            var m = new StackChanEndpoint("1.2.3.4", 81, "x").WithMdnsHost();
            Assert.IsTrue(m.IsMdns);
            Assert.AreEqual(81, m.Port);
            Assert.AreEqual("x", m.Token);
        }

        [Test]
        public void Backoff_DoublesAndCaps()
        {
            var b = new ReconnectBackoff(1.0, 30.0, 2.0);
            Assert.AreEqual(1.0, b.Next().TotalSeconds, 1e-9);
            Assert.AreEqual(2.0, b.Next().TotalSeconds, 1e-9);
            Assert.AreEqual(4.0, b.Next().TotalSeconds, 1e-9);
            for (var i = 0; i < 10; i++) b.Next();
            Assert.AreEqual(30.0, b.Next().TotalSeconds, 1e-9);
            Assert.AreEqual(30.0, b.DelayFor(100000).TotalSeconds, 1e-9);
        }

        [Test]
        public void Backoff_ResetRestarts()
        {
            var b = new ReconnectBackoff(1.0, 30.0, 2.0);
            b.Next();
            b.Next();
            b.Reset();
            Assert.AreEqual(0, b.Attempt);
            Assert.AreEqual(1.0, b.Next().TotalSeconds, 1e-9);
        }

        [Test]
        public void Backoff_RejectsBadArgs()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReconnectBackoff(0, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReconnectBackoff(5, 1));
        }

        [Test]
        public void Messages_Emotion()
        {
            Assert.AreEqual("{\"type\":\"emotion\",\"value\":\"happy\",\"durationMs\":3000}",
                StackChanMessages.Emotion(StackChanEmotion.Happy, 3000));
        }

        [Test]
        public void Messages_ShowQr_AndHelloType()
        {
            Assert.AreEqual("{\"type\":\"showQr\"}", StackChanMessages.ShowQr());
            Assert.AreEqual("{\"type\":\"hideQr\"}", StackChanMessages.HideQr());
            Assert.AreEqual("hello", StackChanMessages.TryGetType("{\"type\":\"hello\",\"mac\":\"x\"}", out _));
            Assert.IsNull(StackChanMessages.TryGetType("nope", out _));
        }
    }
}
