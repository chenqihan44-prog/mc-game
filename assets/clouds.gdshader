shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_opaque;
uniform float cover = 0.35;
uniform vec2 drift = vec2(0.0);
uniform vec3 cloud_color = vec3(0.94);
varying vec2 world_uv;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
void vertex() { world_uv = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz; }
void fragment() {
    vec2 p = floor((world_uv + drift) / 16.0);
    float a = hash(floor(p / 3.0));
    float b = hash(p);
    if(a * 0.75 + b * 0.25 > cover) discard;
    ALBEDO = cloud_color * (0.85 + b * 0.15);
}
