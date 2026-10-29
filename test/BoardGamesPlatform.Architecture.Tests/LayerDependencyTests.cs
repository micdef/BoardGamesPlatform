/*
 *	@file	test/BoardGamesPlatform.Architecture.Tests/LayerDependencyTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the dependencies between the types of the layers, inside the compiled assemblies
 */

// Using
using System.Reflection;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;

// Static using
using static ArchUnitNET.Fluent.ArchRuleDefinition;

// Named using
using ArchitectureModel = ArchUnitNET.Domain.Architecture;

// Namespace
namespace BoardGamesPlatform.Architecture.Tests;

/// <summary>
/// Tests used to check the dependencies between the layers, type by type
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> catches what the project references cannot see: a forbidden dependency through a transitive reference, or a framework
/// (ASP.NET Core, SQL client) used in a layer that must stay independent of it.</para>
/// <para><b>Behavior:</b> loads the compiled assemblies of the solution once, with their direct dependencies (without them, a dependency on an
/// external type such as ASP.NET Core would not be seen), then checks each rule with ArchUnitNET on the platform's own types. A rule whose layer
/// has no type yet passes; it starts guarding the layer as soon as code is written there.</para>
/// </remarks>
public sealed class LayerDependencyTests
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Compiled assemblies of the solution, loaded once for every rule
    /// </summary>
    private static readonly ArchitectureModel _solutionArchitecture = LoadSolutionArchitecture();

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Enumerations

    // #N/A

    #endregion

    #region Services

    // #N/A

    #endregion

    #region Accessors/Properties

    #region Members & Properties

    // #N/A

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    // #N/A

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to load the compiled assemblies of the source projects
    /// </summary>
    /// <returns>
    /// The architecture model of every "BoardGamesPlatform.*" assembly of src/ copied next to the tests
    /// </returns>
    private static ArchitectureModel LoadSolutionArchitecture()
    {
        // Listing the assemblies of the source projects (not the tests nor the TestKit)
        Assembly[] assemblies = Directory.GetFiles(AppContext.BaseDirectory, "BoardGamesPlatform.*.dll")
            .Where(path => !path.EndsWith(".Tests.dll", StringComparison.Ordinal) && !path.EndsWith(".TestKit.dll", StringComparison.Ordinal))
            .Select(Assembly.LoadFrom)
            .ToArray();

        // Function's return: the dependencies are loaded too, so that the rules see the external types used by the platform
        return new ArchLoader().LoadAssembliesIncludingDependencies(assemblies).Build();
    }

    #endregion

    #region Publics

    /// <summary>
    /// The architecture model sees the dependencies on external types (control of the other rules)
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> the rules below forbid external frameworks (ASP.NET Core, SQL client); they only work if the model includes the
    /// dependencies of the assemblies. This control fails if the loading ever stops seeing them, instead of letting the other rules pass blindly.</para>
    /// <para><b>Behavior:</b> ObservabilityExtensions is built on OpenTelemetry, so the model must report that dependency.</para>
    /// </remarks>
    [Fact]
    public void ArchitectureModelSeesExternalDependencies()
    {
        // Defining the control: a dependency that exists for sure
        IArchRule control = Types().That().HaveFullName("BoardGamesPlatform.Observability.ObservabilityExtensions")
            .Should().DependOnAny(Types().That().ResideInNamespaceMatching(@"^OpenTelemetry(\..*)?$"))
            .Because("the Observability library is built on OpenTelemetry");

        // Check
        control.Check(_solutionArchitecture);
    }

    /// <summary>
    /// The platform domain does not depend on the outer layers nor on any infrastructure
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> keeps the rules of the platform testable on their own and independent of the transport and of the database.</para>
    /// </remarks>
    [Fact]
    public void PlatformDomainDependsOnNoOuterLayer()
    {
        // Defining the rule
        IArchRule rule = Types().That().ResideInNamespaceMatching(@"^BoardGamesPlatform\.Platform\.Domain(\..*)?$")
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching(
                @"^(BoardGamesPlatform\.(Platform\.(Application|Infrastructure)|Protocol|Back|VeryBack|Gateway|Web)|Microsoft\.AspNetCore|Microsoft\.Data)(\..*)?$"))
            .Because("the domain is the centre of the Clean Architecture")
            .WithoutRequiringPositiveResults();

        // Check
        rule.Check(_solutionArchitecture);
    }

    /// <summary>
    /// The platform use cases do not depend on their adapters
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> the application layer defines ports; the infrastructure implements them, never the opposite.</para>
    /// </remarks>
    [Fact]
    public void PlatformApplicationDependsOnNoAdapter()
    {
        // Defining the rule
        IArchRule rule = Types().That().ResideInNamespaceMatching(@"^BoardGamesPlatform\.Platform\.Application(\..*)?$")
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching(
                @"^(BoardGamesPlatform\.(Platform\.Infrastructure|Protocol|Back|VeryBack|Gateway|Web)|Microsoft\.AspNetCore|Microsoft\.Data)(\..*)?$"))
            .Because("the use cases only know the ports they define")
            .WithoutRequiringPositiveResults();

        // Check
        rule.Check(_solutionArchitecture);
    }

    /// <summary>
    /// Only the Very Back uses a SQL client
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> the Very Back is the only access to the database.</para>
    /// <para><b>Behavior:</b> the Very Back host is exempt; its contract (BoardGamesPlatform.VeryBack.Contracts), seen by the Back, is not.</para>
    /// </remarks>
    [Fact]
    public void OnlyTheVeryBackUsesASqlClient()
    {
        // Defining the rule
        IArchRule rule = Types().That().ResideInNamespaceMatching(@"^BoardGamesPlatform(\..*)?$")
            .And().DoNotResideInNamespaceMatching(@"^BoardGamesPlatform\.VeryBack(\.(?!Contracts(\.|$)).*)?$")
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching(@"^(Microsoft\.Data\.SqlClient|System\.Data\.SqlClient)(\..*)?$"))
            .Because("only the Very Back reaches the database")
            .WithoutRequiringPositiveResults();

        // Check
        rule.Check(_solutionArchitecture);
    }

    #endregion

    #endregion

    #region Overrides

    #region Methods

    // #N/A

    #endregion

    #region Operators

    // #N/A

    #endregion

    #endregion
}
