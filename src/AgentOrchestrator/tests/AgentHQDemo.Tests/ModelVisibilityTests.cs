using AgentHQDemo.Api.Controllers;

namespace AgentHQDemo.Tests;

/// <summary>
/// Guards the model picker against leaking internal-only models.
/// </summary>
/// <remarks>
/// Mirrors <c>tests/test_model_visibility.py</c> in the Python track.
/// Plain facts rather than theories so both suites report the same test count.
/// </remarks>
public class ModelVisibilityTests
{
    [Fact]
    public void IsInternalOnly_FlagsInternalModels_RegardlessOfCase()
    {
        string[] names =
        [
            "GPT-5.6 Sol Fast (Internal only)",
            "GPT-5.6 Sol Fast (INTERNAL ONLY)",
            "gpt-5.6 sol fast (internal only)"
        ];

        foreach (var name in names)
        {
            Assert.True(ChatController.IsInternalOnly(name), $"'{name}' should be hidden.");
        }
    }

    [Fact]
    public void IsInternalOnly_DoesNotFlagModelsWeActuallyShip()
    {
        // An over-broad pattern would silently hide a real model, so pin the
        // whole static catalog rather than a single example.
        foreach (var model in ChatController.AvailableModels.Values)
        {
            Assert.False(ChatController.IsInternalOnly(model.Name),
                $"'{model.Name}' is a real model but was filtered out of the picker.");
        }
    }

    [Fact]
    public void IsInternalOnly_TreatsMissingNamesAsVisible()
    {
        Assert.False(ChatController.IsInternalOnly(null));
        Assert.False(ChatController.IsInternalOnly(""));
    }
}
