using Arbiter.Core.Aggregates;

namespace Arbiter.Core.Tests;

public class ComponentDataContainerTests
{
    [Test]
    public void Get_returns_new_instance_when_no_data_exists()
    {
        var container = new ComponentDataContainer();

        var result = container.Get<TestData>();

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<TestData>());
    }

    [Test]
    public void Get_returns_same_instance_on_subsequent_calls()
    {
        var container = new ComponentDataContainer();

        var first = container.Get<TestData>();
        var second = container.Get<TestData>();

        Assert.That(ReferenceEquals(first, second), Is.True);
    }

    [Test]
    public void Get_returns_different_instances_for_different_types()
    {
        var container = new ComponentDataContainer();

        var testData = container.Get<TestData>();
        var otherData = container.Get<OtherData>();

        Assert.That(testData, Is.InstanceOf<TestData>());
        Assert.That(otherData, Is.InstanceOf<OtherData>());
        Assert.That(ReferenceEquals(testData, otherData), Is.False);
    }

    [Test]
    public void Get_returns_instance_with_default_state()
    {
        var container = new ComponentDataContainer();

        var result = container.Get<TestData>();

        Assert.That(result.Value, Is.EqualTo("default"));
    }

    private class TestData
    {
        public string Value { get; set; } = "default";
    }

    private class OtherData
    {
        public int Count { get; set; } = 42;
    }
}