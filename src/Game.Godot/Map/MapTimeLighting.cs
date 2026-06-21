using Game.Core.Model;

namespace Game.Godot.Map;

internal static class MapTimeLighting
{
	private static readonly float[] LightOpacities =
	[
		0.4f, 0.4f, 0.5f, 0.5f, 0.6f, 0.7f,
		1.0f, 1.0f, 1.0f, 0.8f, 0.6f, 0.4f,
	];

	public static float GetDimAlpha(TimeSlot timeSlot)
	{
		var index = (int)timeSlot;
		if (index < 0 || index >= LightOpacities.Length)
		{
			throw new ArgumentOutOfRangeException(nameof(timeSlot), timeSlot, "Unsupported time slot.");
		}

		return 1f - LightOpacities[index];
	}
}
