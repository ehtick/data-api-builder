// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.DataApiBuilder.Core.Configurations;
using Azure.DataApiBuilder.Core.Models;

namespace Cli.Tests;
/// <summary>
/// Test for config file initialization.
/// </summary>
[TestClass]
public class ValidateConfigTests
    : VerifyBase
{
    private MockFileSystem? _fileSystem;
    private FileSystemRuntimeConfigLoader? _runtimeConfigLoader;

    [TestInitialize]
    public void TestInitialize()
    {
        _fileSystem = FileSystemUtils.ProvisionMockFileSystem();

        _runtimeConfigLoader = new FileSystemRuntimeConfigLoader(_fileSystem);

        ILoggerFactory loggerFactory = TestLoggerSupport.ProvisionLoggerFactory();

        SetLoggerForCliConfigGenerator(loggerFactory.CreateLogger<ConfigGenerator>());
        SetCliUtilsLogger(loggerFactory.CreateLogger<Utils>());
    }

    [TestCleanup]
    public void TestCleanup()
    {
        _fileSystem = null;
        _runtimeConfigLoader = null;

        // Clear environment variables set in tests.
        Environment.SetEnvironmentVariable($"connection-string", null);
        Environment.SetEnvironmentVariable($"database-type", null);
        Environment.SetEnvironmentVariable($"sp_param1_int", null);
        Environment.SetEnvironmentVariable($"sp_param2_bool", null);
    }

    /// <summary>
    /// This method validates that the IsConfigValid method returns false when the config is invalid.
    /// </summary>
    [TestMethod]
    public void TestConfigWithCustomPropertyAsInvalid()
    {
        ((MockFileSystem)_fileSystem!).AddFile(TEST_RUNTIME_CONFIG_FILE, CONFIG_WITH_CUSTOM_PROPERTIES);

        ValidateOptions validateOptions = new(TEST_RUNTIME_CONFIG_FILE);

        bool isConfigValid = ConfigGenerator.IsConfigValid(validateOptions, _runtimeConfigLoader!, _fileSystem!);
        Assert.IsFalse(isConfigValid);
    }

    /// <summary>
    /// This method verifies that the relationship validation does not cause unhandled
    /// exceptions, and that the errors generated include the expected messaging.
    /// This case is a regression test due to the metadata needed not always being
    /// populated in the SqlMetadataProvider if for example a bad connection string
    /// is given.
    /// </summary>
    [TestMethod]
    public void TestErrorHandlingForRelationshipValidationWithNonWorkingConnectionString()
    {
        // Arrange
        ((MockFileSystem)_fileSystem!).AddFile(TEST_RUNTIME_CONFIG_FILE, COMPLETE_CONFIG_WITH_RELATIONSHIPS_NON_WORKING_CONN_STRING);
        ValidateOptions validateOptions = new(TEST_RUNTIME_CONFIG_FILE);
        StringWriter writer = new();
        // Capture console output to get error messaging.
        Console.SetOut(writer);

        // Act
        ConfigGenerator.IsConfigValid(validateOptions, _runtimeConfigLoader!, _fileSystem!);
        string errorMessage = writer.ToString();

        // Assert
        Assert.IsTrue(errorMessage.Contains(DataApiBuilderException.CONNECTION_STRING_ERROR_MESSAGE));
    }

    /// <summary>
    /// Validates that the IsConfigValid method returns false when a config is passed with
    /// both rest and graphQL disabled globally.
    /// </summary>
    [TestMethod]
    public void TestConfigWithInvalidConfigProperties()
    {
        ((MockFileSystem)_fileSystem!).AddFile(TEST_RUNTIME_CONFIG_FILE, CONFIG_WITH_DISABLED_GLOBAL_REST_GRAPHQL);

        ValidateOptions validateOptions = new(TEST_RUNTIME_CONFIG_FILE);

        bool isConfigValid = ConfigGenerator.IsConfigValid(validateOptions, _runtimeConfigLoader!, _fileSystem!);
        Assert.IsFalse(isConfigValid);
    }

    /// <summary>
    /// This method validates that the IsConfigValid method returns false when the config is empty.
    /// This is to validate that no exceptions are thrown with validate for failures during config deserialization.
    /// </summary>
    [TestMethod]
    public void TestValidateWithEmptyConfig()
    {
        // create an empty config file
        ((MockFileSystem)_fileSystem!).AddFile(TEST_RUNTIME_CONFIG_FILE, string.Empty);

        ValidateOptions validateOptions = new(TEST_RUNTIME_CONFIG_FILE);

        try
        {
            Assert.IsFalse(ConfigGenerator.IsConfigValid(validateOptions, _runtimeConfigLoader!, _fileSystem!));
        }
        catch (Exception ex)
        {
            Assert.Fail($"Unexpected Exception thrown: {ex.Message}");
        }
    }

    /// <summary>
    /// This Test is used to verify that the validate command is able to catch invalid values for the depth-limit property.
    /// </summary>
    [DataTestMethod]
    [DataRow("null", true, DisplayName = "Invalid Value: 'null'. Only integer values are allowed.")]
    [DataRow("20", true, DisplayName = "Invalid Value: '20'. Integer values provided as strings are not allowed.")]
    [DataRow(0, false, DisplayName = "Invalid Value: 0. Only values between 1 and 2147483647 are allowed along with -1.")]
    [DataRow(-2, false, DisplayName = "Invalid Value: -2. Negative values are not allowed except -1.")]
    [DataRow(2147483648, false, DisplayName = "Invalid Value: 2147483648. Only values between 1 and 2147483647 are allowed along with -1.")]
    [DataRow("seven", true, DisplayName = "Invalid Value: 'seven'. Only integer values are allowed.")]
    public void TestValidateConfigFailsWithInvalidGraphQLDepthLimit(object? depthLimit, bool isStringValue)
    {
        string depthLimitSection = isStringValue ? $@"""depth-limit"": ""{depthLimit}""" : $@"""depth-limit"": {depthLimit}";

        string jsonData = TestHelper.GenerateConfigWithGivenDepthLimit(depthLimitSection);

        // create an empty config file
        ((MockFileSystem)_fileSystem!).AddFile(TEST_RUNTIME_CONFIG_FILE, jsonData);

        ValidateOptions validateOptions = new(TEST_RUNTIME_CONFIG_FILE);

        try
        {
            Assert.IsFalse(ConfigGenerator.IsConfigValid(validateOptions, _runtimeConfigLoader!, _fileSystem!));
        }
        catch (Exception ex)
        {
            Assert.Fail($"Unexpected Exception thrown: {ex.Message}");
        }
    }

    /// <summary>
    /// This Test is used to verify that DAB fails when the JWT properties are missing for OAuth based providers
    /// </summary>
    [DataTestMethod]
    [DataRow("AzureAD")]
    [DataRow("EntraID")]
    [DataRow("Custom")]
    public void TestMissingJwtProperties(string authScheme)
    {
        string ConfigWithJwtAuthentication = $"{{{SAMPLE_SCHEMA_DATA_SOURCE}, {RUNTIME_SECTION_JWT_AUTHENTICATION_PLACEHOLDER}, \"entities\": {{ }}}}";
        ConfigWithJwtAuthentication = ConfigWithJwtAuthentication.Replace("<>", authScheme, StringComparison.OrdinalIgnoreCase);

        // create an empty config file
        ((MockFileSystem)_fileSystem!).AddFile(TEST_RUNTIME_CONFIG_FILE, ConfigWithJwtAuthentication);

        ValidateOptions validateOptions = new(TEST_RUNTIME_CONFIG_FILE);

        try
        {
            Assert.IsFalse(ConfigGenerator.IsConfigValid(validateOptions, _runtimeConfigLoader!, _fileSystem!));
        }
        catch (Exception ex)
        {
            Assert.Fail($"Unexpected Exception thrown: {ex.Message}");
        }
    }

    /// <summary>
    /// This Test is used to verify that the validate command is able to catch when data source field or entities field is missing.
    /// </summary>
    [TestMethod]
    public void TestValidateConfigFailsWithNoEntities()
    {
        string ConfigWithoutEntities = $"{{{SAMPLE_SCHEMA_DATA_SOURCE},{RUNTIME_SECTION}}}";

        // create an empty config file
        ((MockFileSystem)_fileSystem!).AddFile(TEST_RUNTIME_CONFIG_FILE, ConfigWithoutEntities);

        ValidateOptions validateOptions = new(TEST_RUNTIME_CONFIG_FILE);

        try
        {
            Assert.IsFalse(ConfigGenerator.IsConfigValid(validateOptions, _runtimeConfigLoader!, _fileSystem!));
        }
        catch (Exception ex)
        {
            Assert.Fail($"Unexpected Exception thrown: {ex.Message}");
        }
    }

    /// <summary>
    /// This Test is used to verify that the validate command is able to catch when data source field is missing.
    /// </summary>
    [TestMethod]
    public void TestValidateConfigFailsWithNoDataSource()
    {
        string ConfigWithoutDataSource = $"{{{SCHEMA_PROPERTY},{RUNTIME_SECTION_WITH_EMPTY_ENTITIES}}}";

        // create an empty config file
        ((MockFileSystem)_fileSystem!).AddFile(TEST_RUNTIME_CONFIG_FILE, ConfigWithoutDataSource);

        ValidateOptions validateOptions = new(TEST_RUNTIME_CONFIG_FILE);

        try
        {
            Assert.IsFalse(ConfigGenerator.IsConfigValid(validateOptions, _runtimeConfigLoader!, _fileSystem!));
        }
        catch (Exception ex)
        {
            Assert.Fail($"Unexpected Exception thrown: {ex.Message}");
        }
    }

    /// <summary>
    /// This method implicitly validates that RuntimeConfigValidator::ValidateConfigSchema(...) successfully
    /// executes against a config file referencing environment variables.
    /// [CLI] ConfigGenerator::IsConfigValid(...)
    ///     |_ [Engine] RuntimeConfigValidator::TryValidateConfig(...)
    ///        |_ [Engine] RuntimeConfigValidator::ValidateConfigSchema(...)
    /// ValidateConfigSchema(...) doesn't execute successfully when a RuntimeConfig object has unresolved environment variables.
    /// Example:
    /// Input file snipppet:
    ///   "data-source": {
    ///     "database-type": "@env('DATABASE_TYPE')", // ENUM
    ///     "connection-string": "@env('CONN_STRING')" // STRING
    ///   }
    ///   ...
    ///   "source": {
    ///     "type": ""stored-procedure",
    ///     "object": "s001.book",
    ///     "parameters": {
    ///         "param1": "@env('sp_param1_int')", // INT
    ///         "param2": "@env('sp_param2_bool')" // BOOL
    ///     }
    ///   }
    /// </summary>
    [TestMethod]
    public void ValidateConfigSchemaWhereConfigReferencesEnvironmentVariables()
    {
        // Arrange
        Environment.SetEnvironmentVariable($"connection-string", SAMPLE_TEST_CONN_STRING);
        Environment.SetEnvironmentVariable($"database-type", "mssql");
        Environment.SetEnvironmentVariable($"sp_param1_int", "123");
        Environment.SetEnvironmentVariable($"sp_param2_bool", "true");

        // Capture console output to get error messaging.
        StringWriter writer = new();
        Console.SetOut(writer);

        ((MockFileSystem)_fileSystem!).AddFile(
            path: TEST_RUNTIME_CONFIG_FILE,
            mockFile: CONFIG_ENV_VARS);
        ValidateOptions validateOptions = new(TEST_RUNTIME_CONFIG_FILE);

        // Act
        ConfigGenerator.IsConfigValid(validateOptions, _runtimeConfigLoader!, _fileSystem!);

        // Assert
        string loggerOutput = writer.ToString();
        Assert.IsFalse(
            condition: loggerOutput.Contains("Failed to validate config against schema due to"),
            message: "Unexpected errors encountered when validating config schema in RuntimeConfigValidator::ValidateConfigSchema(...).");
        Assert.IsTrue(
            condition: loggerOutput.Contains("The config satisfies the schema requirements."),
            message: "RuntimeConfigValidator::ValidateConfigSchema(...) didn't communicate successful config schema validation.");
    }

    /// <summary>
    /// Tests that validation fails when AKV options are configured without an endpoint.
    /// </summary>
    [TestMethod]
    public async Task TestValidateAKVOptionsWithoutEndpointFails()
    {
        // Arrange
        _fileSystem!.AddFile(TEST_RUNTIME_CONFIG_FILE, new MockFileData(INITIAL_CONFIG));
        Assert.IsTrue(_fileSystem!.File.Exists(TEST_RUNTIME_CONFIG_FILE));
        Mock<RuntimeConfigProvider> mockRuntimeConfigProvider = new(_runtimeConfigLoader);
        RuntimeConfigValidator validator = new(mockRuntimeConfigProvider.Object, _fileSystem, new Mock<ILogger<RuntimeConfigValidator>>().Object);
        Mock<ILoggerFactory> mockLoggerFactory = new();
        Mock<ILogger<JsonConfigSchemaValidator>> mockLogger = new();
        mockLoggerFactory
            .Setup(factory => factory.CreateLogger(typeof(JsonConfigSchemaValidator).FullName!))
            .Returns(mockLogger.Object);

        // Act: Attempts to add AKV options
        ConfigureOptions options = new(
            azureKeyVaultRetryPolicyMaxCount: 1,
            azureKeyVaultRetryPolicyDelaySeconds: 1,
            azureKeyVaultRetryPolicyMaxDelaySeconds: 1,
            azureKeyVaultRetryPolicyMode: AKVRetryPolicyMode.Exponential,
            azureKeyVaultRetryPolicyNetworkTimeoutSeconds: 1,
            config: TEST_RUNTIME_CONFIG_FILE
        );

        bool isSuccess = TryConfigureSettings(options, _runtimeConfigLoader!, _fileSystem!);

        // Assert: Settings are configured, config parses, validation fails.
        Assert.IsTrue(isSuccess);
        string updatedConfig = _fileSystem!.File.ReadAllText(TEST_RUNTIME_CONFIG_FILE);
        Assert.IsTrue(RuntimeConfigLoader.TryParseConfig(updatedConfig, out RuntimeConfig? config));
        JsonSchemaValidationResult result = await validator.ValidateConfigSchema(config, TEST_RUNTIME_CONFIG_FILE, mockLoggerFactory.Object);
        Assert.IsFalse(result.IsValid);
    }

    /// <summary>
    /// Tests that validation fails when Azure Log Analytics options are configured without the Auth options.
    /// </summary>
    [TestMethod]
    public async Task TestValidateAzureLogAnalyticsOptionsWithoutAuthFails()
    {
        // Arrange
        _fileSystem!.AddFile(TEST_RUNTIME_CONFIG_FILE, new MockFileData(INITIAL_CONFIG));
        Assert.IsTrue(_fileSystem!.File.Exists(TEST_RUNTIME_CONFIG_FILE));
        Mock<RuntimeConfigProvider> mockRuntimeConfigProvider = new(_runtimeConfigLoader);
        RuntimeConfigValidator validator = new(mockRuntimeConfigProvider.Object, _fileSystem, new Mock<ILogger<RuntimeConfigValidator>>().Object);
        Mock<ILoggerFactory> mockLoggerFactory = new();
        Mock<ILogger<JsonConfigSchemaValidator>> mockLogger = new();
        mockLoggerFactory
            .Setup(factory => factory.CreateLogger(typeof(JsonConfigSchemaValidator).FullName!))
            .Returns(mockLogger.Object);

        // Act: Attempts to add Azure Log Analytics options without Auth options
        ConfigureOptions options = new(
            azureLogAnalyticsEnabled: CliBool.True,
            azureLogAnalyticsLogType: "log-type-test",
            azureLogAnalyticsFlushIntervalSeconds: 1,
            config: TEST_RUNTIME_CONFIG_FILE
        );

        bool isSuccess = TryConfigureSettings(options, _runtimeConfigLoader!, _fileSystem!);

        // Assert: Settings are configured, config parses, validation fails.
        Assert.IsTrue(isSuccess);
        string updatedConfig = _fileSystem!.File.ReadAllText(TEST_RUNTIME_CONFIG_FILE);
        Assert.IsTrue(RuntimeConfigLoader.TryParseConfig(updatedConfig, out RuntimeConfig? config));
        JsonSchemaValidationResult result = await validator.ValidateConfigSchema(config, TEST_RUNTIME_CONFIG_FILE, mockLoggerFactory.Object);
        Assert.IsFalse(result.IsValid);
    }
}
