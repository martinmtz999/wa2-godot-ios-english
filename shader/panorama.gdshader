// Panorama: draws the 1280x720 scene layer across a wider rect, stretching only toward the edges.
// Centre half-width c is untouched; beyond it the stretch grows smoothly (quadratic, no kink).
shader_type canvas_item;
global uniform vec4 pano;
uniform sampler2D scene : filter_linear;
uniform float out_width = 1280.0;     // drawn width in game px (1280 + 2E)

void fragment() {
	float xo = UV.x * out_width - pano.x;          // output x in 1280 space, -E .. 1280+E
	float d = xo - 640.0, ad = abs(d), xs;
	if (ad <= pano.y || pano.x <= 0.0) xs = xo;
	else {
		float v = ad - pano.y;
		float u = (-pano.z + sqrt(pano.z * pano.z + 4.0 * pano.x * v)) / (2.0 * pano.x);
		xs = 640.0 + sign(d) * (pano.y + pano.z * min(u, 1.0));
	}
	COLOR = texture(scene, vec2(clamp(xs / 1280.0, 0.0, 1.0), UV.y));
}
