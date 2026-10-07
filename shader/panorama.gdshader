// Panorama: draws the 1280x720 scene layer across a wider rect. The picture is first widened evenly
// by pre_k (1 for backgrounds, 1.05 for event CGs), then stretched only toward the edges by E: the
// centre half-width c is untouched; beyond it the stretch grows smoothly (quadratic, no kink).
shader_type canvas_item;
global uniform vec4 pano;             // E, c, L (for pre_k = 1), active; also read by mask.gdshader
uniform sampler2D scene : filter_linear;
uniform float out_width = 1280.0;     // drawn width in game px: 1280 * pre_k + 2E
uniform float pre_k = 1.0;
uniform float edge = 0.0;             // E for this rect (pano.x is 0 while pre_k > 1)

void fragment() {
	float hs = 640.0 * pre_k, c = pano.y, l = hs - c;
	float d = UV.x * out_width - out_width * 0.5, ad = abs(d), ds;
	if (ad <= c || edge <= 0.0) ds = d;
	else {
		float v = ad - c;
		float u = (-l + sqrt(l * l + 4.0 * edge * v)) / (2.0 * edge);
		ds = sign(d) * (c + l * min(u, 1.0));
	}
	COLOR = texture(scene, vec2(clamp((640.0 + ds / pre_k) / 1280.0, 0.0, 1.0), UV.y));
}
