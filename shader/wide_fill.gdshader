// Side strips on screens wider than 16:9 (Wa2Wide). Paints outside the centred game rect the
// current frame enlarged to cover the screen and blurred. The seam is soft: at the game edge the
// fill has the picture's own brightness and the blur fades in over the outermost sliver of the
// picture (feather), then darkens gradually toward the screen edge; a faint cyan glow (the text
// box ring colour) replaces the earlier hard line.
shader_type canvas_item;
render_mode unshaded;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform vec4 center = vec4(0.0, 0.0, 1.0, 1.0);   // game rect, normalised x, y, w, h
uniform float aspect = 1.7778;                    // screen width / height
uniform float radius = 0.04;                      // blur radius, fraction of screen height (user: "somewhere in the middle")
uniform float feather = 0.011;                    // blend zone inside the game edge, fraction of width
uniform float outer_dim = 0.5;                    // brightness at the screen edge
uniform vec3 glow_color = vec3(0.55, 0.88, 1.0);
uniform float glow = 0.10;
uniform int taps = 64;                            // 12 in Low Power Mode (Wa2Wide)

vec3 blurred(vec2 g, vec2 lo, vec2 hi) {
	vec3 acc = vec3(0.0);
	for (int i = 0; i < 64; i++) {
		if (i >= taps) break;
		float r = sqrt((float(i) + 0.5) / float(taps)) * radius;
		float a = float(i) * 2.39996;
		vec2 s = clamp(g + vec2(cos(a) / aspect, sin(a)) * r, lo + 0.002, hi - 0.002);
		acc += texture(screen_tex, s).rgb;
	}
	return acc / float(taps);
}

void fragment() {
	vec2 uv = SCREEN_UV;
	vec2 lo = center.xy, hi = center.xy + center.zw;
	float k = max(1.0 / center.z, 1.0 / center.w);
	vec2 g = (uv - 0.5) / k + lo + center.zw * 0.5;
	bool inside = uv.x >= lo.x && uv.x <= hi.x && uv.y >= lo.y && uv.y <= hi.y;
	bool sides = lo.x > 0.0005;
	if (inside) {
		// soft blend over the outermost sliver of the picture (side strips only)
		float d = min(uv.x - lo.x, hi.x - uv.x);
		if (!sides || d > feather) { COLOR = vec4(0.0); }
		else {
			float a = 1.0 - smoothstep(0.0, feather, d);
			COLOR = vec4(blurred(g, lo, hi), a * a * 0.85);
		}
	} else {
		vec3 c = blurred(g, lo, hi);
		if (uv.x < lo.x || uv.x > hi.x) {
			float t = uv.x < lo.x ? 1.0 - uv.x / max(lo.x, 1e-4) : (uv.x - hi.x) / max(1.0 - hi.x, 1e-4);
			c *= mix(1.0, outer_dim, smoothstep(0.0, 1.0, t));
			float dpx = (uv.x < lo.x ? lo.x - uv.x : uv.x - hi.x) / SCREEN_PIXEL_SIZE.x;
			float fade = smoothstep(0.0, 0.3, uv.y) * smoothstep(1.0, 0.7, uv.y);
			c += glow_color * exp(-dpx / 40.0) * glow * fade;
		} else {
			c *= 0.75;   // top/bottom bars (taller screens)
		}
		COLOR = vec4(c, 1.0);
	}
}
