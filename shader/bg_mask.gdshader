shader_type canvas_item;
uniform sampler2D mask_texture;
uniform sampler2D next_texture;
uniform vec2 next_scale=vec2(1.0,1.0);
uniform vec2 next_offset=vec2(0.0,0.0);
uniform float blend:hint_range(0.0, 1.0, 0.01)=0;
uniform float fead_weight:hint_range(0,50)=1;
uniform vec2 canvas_size=vec2(1280,720);
uniform bool center=false;
// MaskImage draws the incoming background during B/V transitions. Like mask.gdshader (used by Bg,
// which shows the same picture once the transition ends) it must halve 2x pictures (Sharper,
// Wa2Image.SetNextTexture sets the divisor) and apply the global colour tone; without these the
// fade showed a 2x picture at double size, then snapped to the real view at the end (2026-10-15).
uniform float next_texture_div=1.0;
global uniform vec3 fb;
varying vec2 cur_texture_size;
varying vec2 mask_texture_size;
varying vec2 next_texture_size;
void vertex() {
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
void fragment() {
	vec4 mask_color=texture(mask_texture,UV);
	vec4 next_color=get_color(next_texture,next_texture_size,UV,next_scale,next_offset);
	if (next_color.a==0.0){
		next_color=vec4(0.0);
	}
	float result=(blend*2.0-(1.0-mask_color.r))*fead_weight;
	float final=clamp(result,0.0,1.0);
	COLOR=fb_color(mix(vec4(0.0),next_color,final));
}
