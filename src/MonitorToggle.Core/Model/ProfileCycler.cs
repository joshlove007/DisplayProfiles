namespace MonitorToggle.Core.Model;

public static class ProfileCycler
{
    /// <summary>The profile after <paramref name="currentId"/> in list order, wrapping around; the first one if none is current.</summary>
    public static Profile? Next(IReadOnlyList<Profile> profiles, Guid? currentId)
    {
        if (profiles.Count == 0)
        {
            return null;
        }

        int index = currentId is { } id ? profiles.ToList().FindIndex(p => p.Id == id) : -1;
        return profiles[(index + 1) % profiles.Count];
    }

    /// <summary>Finds a profile by id, exact name, or unique name prefix (case-insensitive).</summary>
    public static Profile? Resolve(IReadOnlyList<Profile> profiles, string nameOrId)
    {
        if (Guid.TryParse(nameOrId, out var id))
        {
            return profiles.FirstOrDefault(p => p.Id == id);
        }

        var exact = profiles.FirstOrDefault(p => string.Equals(p.Name, nameOrId, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var prefixed = profiles.Where(p => p.Name.StartsWith(nameOrId, StringComparison.OrdinalIgnoreCase)).ToList();
        return prefixed.Count == 1 ? prefixed[0] : null;
    }
}
