shader_type canvas_item;
uniform sampler2D cur_texture;
uniform sampler2D mask_texture;
uniform sampler2D next_texture;
uniform vec2 cur_scale=vec2(1.0,1.0);
uniform vec2 cur_offset=vec2(0.0,0.0);
uniform vec2 next_scale=vec2(1.0,1.0);
uniform vec2 next_offset=vec2(0.0,0.0);
uniform float blend:hint_range(0.0, 1.0, 0.01)=0;
uniform float fead_weight:hint_range(0,50)=1;
uniform vec2 canvas_size=vec2(1280,720);
uniform bool center=false;
// x2 UI copies (Wa2DataPack) report their real pixel size to textureSize(); the swap sets these
// to 2 so layout keeps using the original size. 1 = unchanged behaviour.
uniform float cur_texture_div=1.0;
uniform float next_texture_div=1.0;
global uniform vec3 fb;
// Panorama (Wa2Panorama): x = extra px per side E, y = untouched half-width c, z = ramp length L,
// w = 1 when active. Character layers set prewarp: they are counter-warped so that after the
// panorama stretch they keep their exact shape, centred where the stretch moves them.
global uniform vec4 pano;
uniform bool prewarp = false;
varying vec2 cur_texture_size;
varying vec2 mask_texture_size;
varying vec2 next_texture_size;
void vertex() {
    // 获取纹理大小，Godot 返回 vec2
    cur_texture_size = vec2(textureSize(cur_texture, 0)) / cur_texture_div;
	mask_texture_size = vec2(textureSize(mask_texture, 0));
	next_texture_size = vec2(textureSize(next_texture, 0)) / next_texture_div;
}
vec4 fb_color(vec4 tex_color){
    float bias = length(fb- vec3(0.5)) * 2.0;
    vec3 target_color = vec3(step(0.5, fb));
    vec3 final_color = mix(tex_color.rgb, target_color, clamp(bias*0.6, 0.0, 1.0));
    return vec4(final_color, tex_color.a);
}
vec4 get_color(sampler2D input_texture,vec2 input_size,vec2 uv,vec2 scale,vec2 offset){
	if(input_size==vec2(4,4)){
		return vec4(0);
	}
	vec2 size=vec2(input_size)/canvas_size;
	vec2 scaled_uv = (uv /scale);
	if(!center){
		offset-=vec2(0,0);
	}else{
		offset-=vec2((canvas_size.x-input_size.x)*0.5,canvas_size.y-input_size.y);
	}

    vec2 bg_offset = ((vec2(1.0)-scale)*0.5/scale)-(offset/canvas_size);
    vec2 centered_uv = (scaled_uv - bg_offset)/size;

	if (centered_uv.x > 1.0 || centered_uv.y > 1.0 || centered_uv.x < 0.0 ||centered_uv.y < 0.0) {
		return vec4(0.0);
	}
	vec4 bg_color=texture(input_texture,centered_uv);
	return bg_color;
}
float pano_f(float x) {      // forward stretch, in 1280-wide px
	float d = x - 640.0, ad = abs(d);
	if (ad <= pano.y) return x;
	float u = (ad - pano.y) / pano.z;
	return 640.0 + sign(d) * (pano.y + pano.z * u + pano.x * u * u);
}
vec2 prewarp_uv(vec2 uv, vec2 offset) {
	if (!prewarp || pano.w < 0.5 || pano.x <= 0.0) return uv;
	float xv = uv.x * canvas_size.x;
	float xc = canvas_size.x * 0.5 - offset.x;           // sprite centre (centred layout)
	float xs = xc + (pano_f(xv) - pano_f(xc));            // rigid placement around the warped centre
	return vec2(xs / canvas_size.x, uv.y);
}
void fragment() {
	vec4 mask_color=texture(mask_texture,UV);
	vec4 cur_color=get_color(cur_texture,cur_texture_size,prewarp_uv(UV,cur_offset),cur_scale,cur_offset);
	vec4 next_color=get_color(next_texture,next_texture_size,prewarp_uv(UV,next_offset),next_scale,next_offset);
	if (next_color.a==0.0){
		next_color=vec4(0.0);
	}
	float result=(blend*2.0-(1.0-mask_color.r))*fead_weight;
	float final=clamp(result,0.0,1.0);

	if(cur_color.a==0.0){
		COLOR=next_color*vec4(1.0,1.0,1.0,blend);
	}else if(next_color.a==0.0){
		COLOR=cur_color*vec4(1.0,1.0,1.0,1.0-blend);
	}else{
		COLOR=mix(cur_color,next_color,final);
	}
	COLOR=fb_color(COLOR);
}
