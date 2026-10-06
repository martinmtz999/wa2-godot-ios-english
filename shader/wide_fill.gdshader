// Side strips on screens wider than 16:9 (Wa2Wide). Paints everything outside the centred game
// rect with the current frame enlarged to cover the screen, blurred and darkened toward the outer
// edge, plus a thin cyan glow line along the game's edge in the colour of the text box ring.
// This is the first version: the user tried a softer seam and a medium blur and preferred this.
shader_type canvas_item;
render_mode unshaded;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform vec4 center = vec4(0.0, 0.0, 1.0, 1.0);   // game rect, normalised x, y, w, h
uniform float aspect = 1.7778;                    // screen width / height
uniform float dim = 0.78;
uniform float radius = 0.025;                    // blur radius, fraction of screen height (Wa2Wide.StripBlur)
uniform vec3 line_color = vec3(0.55, 0.88, 1.0);
uniform int taps = 64;
uniform float outer = 0.72;                       // brightness at the screen edge (Deep fades to near black)                            // 12 in Low Power Mode (Wa2Wide)

void fragment() {
	vec2 uv = SCREEN_UV;
	vec2 lo = center.xy, hi = center.xy + center.zw;
	if (uv.x >= lo.x && uv.x <= hi.x && uv.y >= lo.y && uv.y <= hi.y) {
		COLOR = vec4(0.0);
	} else {
		float k = max(1.0 / center.z, 1.0 / center.w);
		vec2 g = (uv - 0.5) / k + lo + center.zw * 0.5;
		vec3 acc = vec3(0.0);
		for (int i = 0; i < 64; i++) {
			if (i >= taps) break;
			float r = sqrt((float(i) + 0.5) / float(taps)) * radius;
			float a = float(i) * 2.39996;
			vec2 s = clamp(g + vec2(cos(a) / aspect, sin(a)) * r, lo + 0.002, hi - 0.002);
			acc += texture(screen_tex, s).rgb;
		}
		vec3 c = acc / float(taps) * dim;
		bool side = uv.x < lo.x || uv.x > hi.x;
		if (side) {
			float edge = uv.x < lo.x ? uv.x / max(lo.x, 1e-4) : (1.0 - uv.x) / max(1.0 - hi.x, 1e-4);
			c *= mix(outer, 1.0, smoothstep(0.0, 1.0, edge));
			float dpx = (uv.x < lo.x ? lo.x - uv.x : uv.x - hi.x) / SCREEN_PIXEL_SIZE.x;
			float line = exp(-dpx * dpx / 3.0) * 0.85 + exp(-dpx / 14.0) * 0.16;
			float fade = smoothstep(0.0, 0.22, uv.y) * smoothstep(1.0, 0.78, uv.y);
			c += line_color * line * fade;
		}
		COLOR = vec4(c, 1.0);
	}
}
