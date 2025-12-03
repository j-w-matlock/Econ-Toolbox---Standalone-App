using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EconToolbox.Desktop.Tests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class STATestMethodAttribute : TestMethodAttribute
{
    public override TestResult[] Execute(ITestMethod testMethod)
    {
        TestResult[]? result = null;
        var thread = new Thread(() => result = base.Execute(testMethod))
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        return result!;
    }
}
