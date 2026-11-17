using DisplayProfiles.Core.Model;

namespace DisplayProfiles.Tests;

public class ProfileComparisonTests
{
    private static Profile Make(Guid id, string name, params MonitorConfig[] monitors) =>
        new() { Id = id, Name = name, Monitors = monitors };

    [Fact]
    public void A_profile_changed_and_changed_back_matches_the_saved_one()
    {
        var id = Guid.NewGuid();
        var saved = Make(id, "Desk", TestData.Mon("A", 0, 0, primary: true), TestData.Mon("B", 2560, 0));

        var edited = Make(id, "Desk", TestData.Mon("A", 0, 0, primary: true), TestData.Mon("B", 2560, 0) with { Width = 1920, Height = 1080 });
        Assert.False(edited.HasSameContentAs(saved));

        var undone = Make(id, "Desk", TestData.Mon("A", 0, 0, primary: true), TestData.Mon("B", 2560, 0));
        Assert.True(undone.HasSameContentAs(saved));
    }

    [Fact]
    public void Order_of_the_monitors_does_not_matter()
    {
        var id = Guid.NewGuid();
        var one = Make(id, "Desk", TestData.Mon("A", 0, 0), TestData.Mon("B", 2560, 0));
        var other = Make(id, "Desk", TestData.Mon("B", 2560, 0), TestData.Mon("A", 0, 0));

        Assert.True(one.HasSameContentAs(other));
    }

    [Fact]
    public void A_different_name_or_a_different_set_of_monitors_is_a_change()
    {
        var id = Guid.NewGuid();
        var saved = Make(id, "Desk", TestData.Mon("A", 0, 0));

        Assert.False(Make(id, "Desk 2", TestData.Mon("A", 0, 0)).HasSameContentAs(saved));
        Assert.False(Make(id, "Desk", TestData.Mon("A", 0, 0), TestData.Mon("B", 2560, 0)).HasSameContentAs(saved));
        Assert.False(Make(id, "Desk", TestData.Dup("A", "B")).HasSameContentAs(saved));
    }
}
