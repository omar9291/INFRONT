using System;
using NUnit.Framework;

namespace Infront.Tests
{
    public sealed class FrameStatisticsTests
    {
        [Test]
        public void Tiefpunkt_mittelt_Bildzeiten_und_nicht_FPS()
        {
            var times = new float[200];
            for (int i = 0; i < times.Length; i++) times[i] = 1f / 60f;
            times[0] = 0.020f;
            times[1] = 0.040f;
            var result = FrameStatistics.Calculate(times);
            Assert.AreEqual(200, result.frames);
            Assert.AreEqual(1000.0 / 30.0, result.onePercentLowFps, 0.001);
            Assert.AreEqual(40, result.maxMilliseconds, 0.001);
            Assert.AreEqual(200.0 / (198.0 / 60 + 0.06), result.averageFps, 0.001);
        }

        [Test]
        public void Ungueltige_Zeiten_verfaelschen_die_Messung_nicht()
        {
            var result = FrameStatistics.Calculate(new[] { 0f, -1f, float.NaN, float.PositiveInfinity, 0.025f });
            Assert.AreEqual(1, result.frames);
            Assert.AreEqual(40, result.averageFps, 0.001);
            Assert.AreEqual(0, FrameStatistics.Calculate(Array.Empty<float>()).frames);
        }

        [Test]
        public void Statistik_veraendert_die_Rohdaten_nicht()
        {
            var times = new[] { 0.04f, 0.01f, 0.03f };
            FrameStatistics.Calculate(times);
            CollectionAssert.AreEqual(new[] { 0.04f, 0.01f, 0.03f }, times);
        }
    }
}
