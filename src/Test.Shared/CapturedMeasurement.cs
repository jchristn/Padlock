namespace Test.Shared
{
	/// <summary>
	/// One metric measurement captured by <see cref="TelemetryCapture"/>.
	/// </summary>
	public sealed class CapturedMeasurement
	{
		/// <summary>
		/// Instrument name.
		/// </summary>
		public string Instrument { get; }

		/// <summary>
		/// Measured value.
		/// </summary>
		public double Value { get; }

		/// <summary>
		/// Measurement tags.
		/// </summary>
		public IReadOnlyDictionary<string, object?> Tags { get; }

		/// <summary>
		/// Creates a captured measurement.
		/// </summary>
		/// <param name="instrument">Instrument name.</param>
		/// <param name="value">Measured value.</param>
		/// <param name="tags">Measurement tags.</param>
		public CapturedMeasurement(string instrument, double value, IReadOnlyDictionary<string, object?> tags)
		{
			Instrument = instrument;
			Value = value;
			Tags = tags;
		}

		/// <summary>
		/// Returns the tag value as a string, or null when absent.
		/// </summary>
		/// <param name="key">Tag key.</param>
		/// <returns>Tag value as a string, or null.</returns>
		public string? Tag(string key)
		{
			if (Tags.TryGetValue(key, out object? value) && value != null)
			{
				return value is bool flag ? (flag ? "true" : "false") : value.ToString();
			}
			return null;
		}
	}
}
