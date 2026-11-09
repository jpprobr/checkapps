using CheckApps.Application.Analysis;
using CheckApps.Domain.Analysis;
using static CheckApps.Application.Analysis.ContractChangeKinds;
using static CheckApps.Application.Analysis.ContractChangeSeverities;

namespace CheckApps.UnitTests.Analysis;

/// <summary>A tabela de classificação da spec 021, linha a linha, e as proteções contra falso alarme.</summary>
public class ContractComparerTests
{
    private const string Request = ApiFieldLocations.Request;
    private const string Response = ApiFieldLocations.Response;
    private const string Query = ApiFieldLocations.Query;

    private static RepositoryApiField Field(
        string location, string path = "campo", string? type = "string", bool required = false, string? format = null, params string[] enumValues) =>
        new()
        {
            Id = Guid.NewGuid().ToString("n"), EndpointId = "e", Location = location, Path = path, Type = type, Format = format,
            Required = required, EnumValues = enumValues,
            StatusCode = location == Response ? "200" : null,
            MediaType = location is Response or Request ? "application/json" : null
        };

    private static RepositoryApiEndpoint Endpoint(
        string method = "POST", string path = "/a", bool deprecated = false, string file = "openapi.yaml", string version = "1.0.0",
        params RepositoryApiField[] fields) =>
        new()
        {
            Id = Guid.NewGuid().ToString("n"), AnalysisId = "an", Mechanism = ApiMechanisms.OpenApi, Method = method, Path = path,
            PathKey = ApiPath.KeyOf(path), Deprecated = deprecated, SourceFile = file, Confidence = Confidences.High,
            ScannerId = "openapi", ScannerVersion = version, Fields = fields
        };

    private static ContractChange Single(RepositoryApiEndpoint before, RepositoryApiEndpoint after, bool anyPartial = false) =>
        Assert.Single(ContractComparer.Compare([before], [after], anyPartial).Changes);

    private static void AssertChange(ContractChange change, string severity, string kind)
    {
        Assert.Equal(severity, change.Severity);
        Assert.Equal(kind, change.Kind);
    }

    [Fact]
    public void NothingChanged_NoChanges()
    {
        var comparison = ContractComparer.Compare(
            [Endpoint(fields: Field(Request, required: true))], [Endpoint(fields: Field(Request, required: true))], anyPartial: false);

        Assert.Empty(comparison.Changes);
        Assert.Empty(comparison.Warnings);
    }

    [Fact]
    public void EndpointRemoved_Breaks()
    {
        var comparison = ContractComparer.Compare([Endpoint(path: "/a")], [], anyPartial: false);

        AssertChange(Assert.Single(comparison.Changes), Breaking, EndpointRemoved);
    }

    [Fact]
    public void EndpointAdded_IsSafe()
    {
        var comparison = ContractComparer.Compare([], [Endpoint(path: "/a", fields: Field(Request, required: true))], anyPartial: false);

        // Um endpoint novo é uma mudança só: os campos dele não entram um a um.
        AssertChange(Assert.Single(comparison.Changes), Safe, EndpointAdded);
    }

    [Theory]
    [InlineData(Request, true, Breaking)]
    [InlineData(Query, true, Breaking)]
    [InlineData(Response, true, Safe)]
    [InlineData(Request, false, Safe)]
    [InlineData(Response, false, Safe)]
    public void ClassifiesFieldAdded(string location, bool required, string severity) =>
        AssertChange(Single(Endpoint(), Endpoint(fields: Field(location, required: required))), severity, FieldAdded);

    [Theory]
    [InlineData(Request, Breaking)]
    [InlineData(Query, Breaking)]
    [InlineData(Response, Safe)]
    public void OptionalBecameRequired(string location, string severity) =>
        AssertChange(Single(Endpoint(fields: Field(location)), Endpoint(fields: Field(location, required: true))), severity, BecameRequired);

    [Theory]
    [InlineData(Request, Safe)]
    [InlineData(Response, Attention)]
    public void RequiredBecameOptional(string location, string severity) =>
        AssertChange(Single(Endpoint(fields: Field(location, required: true)), Endpoint(fields: Field(location))), severity, BecameOptional);

    [Theory]
    [InlineData(Request, Attention)]
    [InlineData(Query, Attention)]
    [InlineData(Response, Breaking)]
    public void ClassifiesFieldRemoved(string location, string severity) =>
        AssertChange(Single(Endpoint(fields: Field(location)), Endpoint()), severity, FieldRemoved);

    [Theory]
    [InlineData(Request)]
    [InlineData(Response)]
    public void TypeChanged_Breaks(string location) =>
        AssertChange(Single(Endpoint(fields: Field(location, type: "number")), Endpoint(fields: Field(location, type: "string"))), Breaking, TypeChanged);

    [Fact]
    public void UnknownType_DoesNotProveAChange_SoItIsAttention() =>
        AssertChange(Single(Endpoint(fields: Field(Response, type: null)), Endpoint(fields: Field(Response, type: "string"))), Attention, TypeChanged);

    [Theory]
    [InlineData(Request)]
    [InlineData(Response)]
    public void FormatChanged_Breaks(string location) =>
        AssertChange(Single(Endpoint(fields: Field(location, format: "int32")), Endpoint(fields: Field(location, format: "int64"))), Breaking, FormatChanged);

    [Theory]
    [InlineData(Request, Breaking)]
    [InlineData(Query, Breaking)]
    [InlineData(Response, Safe)]
    public void ClassifiesEnumValueRemoved(string location, string severity) =>
        AssertChange(Single(
            Endpoint(fields: Field(location, enumValues: ["a", "b"])),
            Endpoint(fields: Field(location, enumValues: ["a"]))), severity, EnumValueRemoved);

    [Theory]
    [InlineData(Request, Safe)]
    [InlineData(Response, Attention)]
    public void ClassifiesEnumValueAdded(string location, string severity) =>
        AssertChange(Single(
            Endpoint(fields: Field(location, enumValues: ["a"])),
            Endpoint(fields: Field(location, enumValues: ["a", "b"]))), severity, EnumValueAdded);

    [Fact]
    public void MarkedAsDeprecated_IsAttention_AndTheOppositeIsNotAChange()
    {
        AssertChange(Single(Endpoint(), Endpoint(deprecated: true)), Attention, EndpointDeprecated);
        Assert.Empty(ContractComparer.Compare([Endpoint(deprecated: true)], [Endpoint()], anyPartial: false).Changes);
    }

    // ---- Identidade ---------------------------------------------------------------------------------------------------

    [Fact]
    public void RenamedPathParameter_IsTheSameEndpoint() =>
        Assert.Empty(ContractComparer.Compare([Endpoint(path: "/a/{id}")], [Endpoint(path: "/a/{fundoId}")], anyPartial: false).Changes);

    [Fact]
    public void RenamedPathParameter_IsNotAChangeInTheField() =>
        Assert.Empty(ContractComparer.Compare(
            [Endpoint(path: "/a/{id}/b/{item}", fields: [Field(ApiFieldLocations.Path, "id", required: true), Field(ApiFieldLocations.Path, "item", required: true)])],
            [Endpoint(path: "/a/{fundoId}/b/{itemId}", fields: [Field(ApiFieldLocations.Path, "fundoId", required: true), Field(ApiFieldLocations.Path, "itemId", required: true)])],
            anyPartial: false).Changes);

    [Fact]
    public void RenamedPathParameter_StillShowsARealChangeInIt() =>
        AssertChange(
            Single(
                Endpoint(path: "/a/{id}", fields: Field(ApiFieldLocations.Path, "id", type: "string", required: true)),
                Endpoint(path: "/a/{fundoId}", fields: Field(ApiFieldLocations.Path, "fundoId", type: "integer", required: true))),
            Breaking, TypeChanged);

    [Fact]
    public void AnotherMethodOnTheSamePath_IsAnotherEndpoint()
    {
        var changes = ContractComparer.Compare([Endpoint(method: "GET")], [Endpoint(method: "POST")], anyPartial: false).Changes;

        Assert.Equal(2, changes.Count);
    }

    [Fact]
    public void RenamedField_IsARemovalAndAnAddition_WithoutGuessing()
    {
        var changes = ContractComparer.Compare(
            [Endpoint(fields: Field(Response, "valorIof"))], [Endpoint(fields: Field(Response, "valorIOF"))], anyPartial: false).Changes;

        Assert.Contains(changes, c => c.Kind == FieldRemoved && c.FieldPath == "valorIof");
        Assert.Contains(changes, c => c.Kind == FieldAdded && c.FieldPath == "valorIOF");
    }

    [Fact]
    public void SamePathInTwoFiles_IsComparedFileByFile_WithAWarning()
    {
        RepositoryApiEndpoint[] before = [Endpoint(file: "v1.yaml"), Endpoint(file: "v2.yaml")];
        RepositoryApiEndpoint[] after = [Endpoint(file: "v1.yaml"), Endpoint(file: "v2.yaml", fields: Field(Request, required: true))];

        var comparison = ContractComparer.Compare(before, after, anyPartial: false);

        Assert.Single(comparison.Changes);
        Assert.Single(comparison.Warnings);
    }

    // ---- Proteção contra falso alarme ----------------------------------------------------------------------------------

    [Fact]
    public void PartialAnalysis_WhatDisappearedIsNeverBreaking()
    {
        var removedEndpoint = ContractComparer.Compare([Endpoint(path: "/a")], [], anyPartial: true);
        AssertChange(Assert.Single(removedEndpoint.Changes), Attention, EndpointRemoved);
        Assert.Contains("parcial", removedEndpoint.Changes[0].Description, StringComparison.Ordinal);

        var removedField = Single(Endpoint(fields: Field(Response)), Endpoint(), anyPartial: true);
        AssertChange(removedField, Attention, FieldRemoved);
        Assert.Contains("parcial", removedField.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void PartialAnalysis_WhatWasReadOnBothSidesStillBreaks() =>
        AssertChange(
            Single(Endpoint(fields: Field(Request)), Endpoint(fields: Field(Request, required: true)), anyPartial: true),
            Breaking, BecameRequired);

    [Fact]
    public void DifferentScannerVersions_Warn()
    {
        var comparison = ContractComparer.Compare([Endpoint(version: "1.0.0")], [Endpoint(version: "2.0.0")], anyPartial: false);

        Assert.Empty(comparison.Changes);
        Assert.Contains(comparison.Warnings, w => w.Contains("scanner", StringComparison.Ordinal));
    }

    [Fact]
    public void BreakingChangesComeFirst()
    {
        var changes = ContractComparer.Compare(
            [Endpoint(path: "/z"), Endpoint(path: "/m", fields: Field(Response, required: true))],
            [Endpoint(path: "/a"), Endpoint(path: "/m")],
            anyPartial: false).Changes;

        Assert.Equal(Breaking, changes[0].Severity);
        Assert.Equal(Safe, changes[^1].Severity);
    }
}
