namespace R3.DynamicData;

/// <summary>
/// Represents a request for a virtual page of data.
/// </summary>
public readonly struct VirtualRequest : IEquatable<VirtualRequest>
{
    /// <summary>
    /// Gets the starting index of the virtual page.
    /// </summary>
    public int StartIndex { get; }

    /// <summary>
    /// Gets the size of the virtual page.
    /// </summary>
    public int Size { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualRequest"/> struct.
    /// </summary>
    /// <param name="startIndex">The starting index.</param>
    /// <param name="size">The page size.</param>
    public VirtualRequest(int startIndex, int size)
    {
        if (startIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startIndex));
        }

        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        StartIndex = startIndex;
        Size = size;
    }

    /// <inheritdoc/>
    public bool Equals(VirtualRequest other) =>
        StartIndex == other.StartIndex && Size == other.Size;

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is VirtualRequest other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(StartIndex, Size);

    /// <summary>
    /// Determines whether two specified instances of <see cref="VirtualRequest"/> are equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns>true if left and right are equal; otherwise, false.</returns>
    public static bool operator ==(VirtualRequest left, VirtualRequest right) =>
        left.Equals(right);

    /// <summary>
    /// Determines whether two specified instances of <see cref="VirtualRequest"/> are not equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns>true if left and right are not equal; otherwise, false.</returns>
    public static bool operator !=(VirtualRequest left, VirtualRequest right) =>
        !left.Equals(right);
}
