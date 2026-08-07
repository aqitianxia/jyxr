shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear;
uniform int preset = 0;
uniform float strength : hint_range(0.0, 1.0) = 0.0;
uniform vec2 screen_offset = vec2(0.0);

vec3 apply_filter(vec3 color) {
	if (preset == 0) {
		float luminance = dot(color, vec3(0.299, 0.587, 0.114));
		return vec3(luminance);
	}

	if (preset == 1) {
		return clamp(vec3(
			dot(color, vec3(0.393, 0.769, 0.189)),
			dot(color, vec3(0.349, 0.686, 0.168)),
			dot(color, vec3(0.272, 0.534, 0.131))
		), 0.0, 1.0);
	}

	if (preset == 2) {
		return clamp(color * vec3(0.82, 0.94, 1.16), 0.0, 1.0);
	}

	if (preset == 3) {
		return clamp(color * vec3(1.14, 1.02, 0.82), 0.0, 1.0);
	}

	if (preset == 4) {
		float luminance = dot(color, vec3(0.299, 0.587, 0.114));
		return clamp(mix(color, vec3(luminance * 0.45, luminance * 1.08, luminance * 0.38), 0.72), 0.0, 1.0);
	}

	return clamp(color * vec3(0.34, 0.48, 0.72), 0.0, 1.0);
}

void fragment() {
	vec2 half_pixel = SCREEN_PIXEL_SIZE * 0.5;
	vec2 sample_uv = clamp(
		SCREEN_UV + screen_offset * SCREEN_PIXEL_SIZE,
		half_pixel,
		vec2(1.0) - half_pixel
	);
	vec4 source = texture(screen_texture, sample_uv);
	COLOR = vec4(mix(source.rgb, apply_filter(source.rgb), strength), source.a);
}
