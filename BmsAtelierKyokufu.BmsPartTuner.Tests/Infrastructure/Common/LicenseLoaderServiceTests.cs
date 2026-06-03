using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Common;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Infrastructure.Common
{
    public class LicenseLoaderServiceTests
    {
        [Fact]
        public void LoadLicenses_ShouldLoadAllEmbeddedLicenses()
        {
            // Act
            var licenses = LicenseLoaderService.LoadLicenses().ToList();

            // Assert
            Assert.NotEmpty(licenses);

            // First item should be the application license
            var firstLicense = licenses[0];
            Assert.True(firstLicense.IsAppLicense);
            Assert.Equal("Bms Part Tuner", firstLicense.Name);
            Assert.Contains("BMS Part Tuner ソフトウェア使用許諾ライセンス", firstLicense.Content);

            // Verify that redirected licenses (MIT templates) are loaded and resolved
            var redirectedLibraries = new[]
            {
                new { Name = "Scrutor", Copyright = "Copyright (c) 2015 Kristian Hellang" },
                new { Name = "MathNet.Numerics", Copyright = "Copyright (c) 2002-2022 Math.NET" },
                new { Name = "NAudio.Vorbis", Copyright = "Copyright (c) 2012 Yuval Naveh / Mark Heath" },
                new { Name = "Flexoki", Copyright = "Copyright (c) 2023 Steph Ango" }
            };

            foreach (var lib in redirectedLibraries)
            {
                var libLicense = licenses.FirstOrDefault(x => x.Name == lib.Name);
                Assert.NotNull(libLicense);
                Assert.False(libLicense.IsAppLicense);

                // Content should contain both the generic MIT text and the injected specific copyright

                Assert.Contains("The MIT License (MIT)", libLicense.Content);
                Assert.Contains(lib.Copyright, libLicense.Content);
                Assert.DoesNotContain("[Copyright]", libLicense.Content);
            }
        }
    }
}
