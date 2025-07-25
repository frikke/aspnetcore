// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using System.Net.Http;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Server.IIS.FunctionalTests.Utilities;
using Microsoft.AspNetCore.Server.IntegrationTesting;
using Microsoft.AspNetCore.Server.IntegrationTesting.IIS;
using Microsoft.AspNetCore.InternalTesting;
using Xunit;

#if !IIS_FUNCTIONALS
using Microsoft.AspNetCore.Server.IIS.FunctionalTests;

#if IISEXPRESS_FUNCTIONALS
namespace Microsoft.AspNetCore.Server.IIS.IISExpress.FunctionalTests;
#elif NEWHANDLER_FUNCTIONALS
namespace Microsoft.AspNetCore.Server.IIS.NewHandler.FunctionalTests;
#elif NEWSHIM_FUNCTIONALS
namespace Microsoft.AspNetCore.Server.IIS.NewShim.FunctionalTests;
#endif

#else
namespace Microsoft.AspNetCore.Server.IIS.FunctionalTests;
#endif

[Collection(PublishedSitesCollection.Name)]
public class WindowsAuthTests : IISFunctionalTestBase
{
    public WindowsAuthTests(PublishedSitesFixture fixture) : base(fixture)
    {
    }

    public static TestMatrix TestVariants
        => TestMatrix.ForServers(DeployerSelector.ServerType)
            .WithTfms(Tfm.Default)
            .WithApplicationTypes(ApplicationType.Portable)
            .WithAllHostingModels();

    [ConditionalTheory]
    [RequiresIIS(IISCapability.WindowsAuthentication)]
    [MemberData(nameof(TestVariants))]
    public async Task WindowsAuthTest(TestVariant variant)
    {
        var deploymentParameters = Fixture.GetBaseDeploymentParameters(variant);
        deploymentParameters.SetAnonymousAuth(enabled: false);
        deploymentParameters.SetWindowsAuth();

        // The default in hosting sets windows auth to true.
        var deploymentResult = await DeployAsync(deploymentParameters);

        var client = deploymentResult.CreateClient(new HttpClientHandler { UseDefaultCredentials = true });
        var response = await client.GetAsync("/Auth");
        var responseText = await response.Content.ReadAsStringAsync();

        Assert.StartsWith("Windows:", responseText);
        Assert.Contains(Environment.UserName, responseText);
    }

    [ConditionalTheory]
    [RequiresIIS(IISCapability.WindowsAuthentication)]
    [MemberData(nameof(TestVariants))]
    public async Task WindowsAuthWithImpersonationLevelTest(TestVariant variant)
    {
        var deploymentParameters = Fixture.GetBaseDeploymentParameters(variant);
        deploymentParameters.SetAnonymousAuth(enabled: false);
        deploymentParameters.SetWindowsAuth();

        // The default in hosting sets windows auth to true.
        var deploymentResult = await DeployAsync(deploymentParameters);

        var impersonationLevels = new TokenImpersonationLevel[]
            {
                TokenImpersonationLevel.None,
                TokenImpersonationLevel.Identification,
                TokenImpersonationLevel.Impersonation,
                TokenImpersonationLevel.Delegation,
                TokenImpersonationLevel.Anonymous
            };

        foreach (var impersonationLevel in impersonationLevels)
        {
            // TokenImpersonationLevel is not supported by HttpClient so we need to use HttpWebRequest to test it.
#pragma warning disable SYSLIB0014 // Type or member is obsolete
            var request = HttpWebRequest.CreateHttp($"{deploymentResult.HttpClient.BaseAddress}Auth");
#pragma warning restore SYSLIB0014 // Type or member is obsolete
            request.ImpersonationLevel = impersonationLevel;
            request.Method = "GET";
            request.UseDefaultCredentials = true;

            using var response = request.GetResponse();
            using var reader = new StreamReader(response.GetResponseStream());
            var responseText = await reader.ReadToEndAsync();

            try
            {
                Assert.StartsWith("Windows:", responseText);
                Assert.Contains(Environment.UserName, responseText);
            }
            catch (Exception ex)
            {
                Assert.Fail($"'TokenImpersonationLevel.{impersonationLevel}' failed with: {ex.Message}");
            }
        }
    }
}
