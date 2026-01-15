using System;
using System.IO;
using EconToolbox.Desktop.Models;
using EconToolbox.Desktop.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EconToolbox.Desktop.Tests;

[TestClass]
public class LayoutSettingsServiceTests
{
    [TestMethod]
    public void Load_ReturnsDefaults_WhenFileMissing()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "EconToolbox.Tests", Guid.NewGuid().ToString("N"));
        var service = new LayoutSettingsService(tempFolder);

        var settings = service.Load();

        Assert.AreEqual(280, settings.ExplorerPaneWidth, 0.1);
        Assert.AreEqual(340, settings.DetailsPaneWidth, 0.1);
        Assert.IsTrue(settings.IsExplorerPaneVisible);
        Assert.IsTrue(settings.IsDetailsPaneVisible);
    }

    [TestMethod]
    public void Save_Persists_LayoutSettings()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "EconToolbox.Tests", Guid.NewGuid().ToString("N"));
        var service = new LayoutSettingsService(tempFolder);
        var settings = new LayoutSettings
        {
            ExplorerPaneWidth = 300,
            DetailsPaneWidth = 360,
            IsExplorerPaneVisible = false,
            IsDetailsPaneVisible = true
        };

        service.Save(settings);
        var loaded = service.Load();

        Assert.AreEqual(300, loaded.ExplorerPaneWidth, 0.1);
        Assert.AreEqual(360, loaded.DetailsPaneWidth, 0.1);
        Assert.IsFalse(loaded.IsExplorerPaneVisible);
        Assert.IsTrue(loaded.IsDetailsPaneVisible);

        if (Directory.Exists(tempFolder))
        {
            Directory.Delete(tempFolder, true);
        }
    }
}
