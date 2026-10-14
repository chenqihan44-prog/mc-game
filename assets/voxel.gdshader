shader_type spatial;
render_mode cull_disabled, ambient_light_disabled, specular_disabled;
uniform float daylight = 1.0;
varying vec3 world_position;
varying vec4 tint;
varying vec3 face_normal;
float grain(vec3 p) { return fract(sin(dot(p, vec3(12.9898, 78.233, 37.719))) * 43758.5453); }
void vertex() {
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    tint = COLOR;
    face_normal = NORMAL;
}
void fragment() {
    vec3 pos = world_position - face_normal * 0.002;
    vec2 uv = abs(face_normal.y) > 0.5 ? pos.xz : (abs(face_normal.x) > 0.5 ? pos.zy : pos.xy);
    vec2 p = floor(fract(uv) * 16.0);
    float noise = grain(floor(pos * 16.0));
    int style = int(floor(tint.a * 255.0 + 0.5));
    float shade = face_normal.y > 0.5 ? 1.0 : (face_normal.y < -0.5 ? 0.58 : 0.82);
    vec3 base = tint.rgb;
    float detail = 0.90 + noise * 0.16;
    if (style == 1) {
        if (face_normal.y < 0.5) {
            float fringe = 12.0 + floor(grain(vec3(p.x, floor(pos.x + pos.z), 3.0)) * 3.0);
            if (face_normal.y < -0.5 || p.y < fringe) base = vec3(0.50, 0.38, 0.30) * shade;
        }
    } else if (style == 2 || style == 18) {
        if (style == 2 && abs(face_normal.y) > 0.5) {
            float rings = mod(floor(length(p - vec2(7.5)) * 1.4), 3.0);
            detail = rings < 1.0 ? 0.65 : 1.15; base *= 1.13;
        } else {
            detail *= mod(p.x + floor(grain(vec3(floor(p.y / 4.0), p.x, 1.0)) * 2.0), 4.0) < 1.0 ? 0.67 : 1.06;
        }
    } else if (style == 19) {
        float ring = mod(floor(length(vec2(fract(uv.x) - 0.5, fract(uv.y) - 0.17)) * 40.0), 3.0);
        detail = ring < 1.0 ? 0.63 : 1.12;
    } else if (style == 3 || style == 11 || style == 12 || style == 14) {
        float row = floor(p.y / 4.0);
        bool seam = mod(p.y, 4.0) < 1.0 || mod(p.x + mod(row, 2.0) * 8.0, 16.0) < 1.0;
        if (seam) detail *= 0.64;
        if (style == 11 && face_normal.y > 0.5) {
            base *= 1.08; if (mod(p.x, 5.0) < 1.0 || mod(p.y, 5.0) < 1.0) detail *= 0.45;
        }
        if (style == 12 && abs(face_normal.y) < 0.5) {
            if (p.x < 2.0 || p.x > 13.0 || p.y < 2.0 || p.y == 11.0) detail *= 0.52;
            if (p.x >= 7.0 && p.x <= 9.0 && p.y >= 6.0 && p.y <= 10.0) {base = vec3(0.84, 0.77, 0.55) * shade;detail = 1.0;}
        }
        if (style == 14 && abs(face_normal.y) < 0.5 && mod(p.y, 8.0) >= 2.0) {
            float book = grain(vec3(floor(p.x / 2.0), floor(p.y / 8.0), 5.0));
            base = mix(vec3(0.28, 0.44, 0.56), vec3(0.68, 0.26, 0.22), book) * shade;
            if (mod(p.x, 2.0) < 1.0 || mod(p.y, 8.0) == 3.0) detail *= 1.25;
        }
    } else if (style == 4 || style == 20 || style == 29 || style == 32) {
        float height = style == 20 ? 5.0 : 4.0;
        float row = floor(p.y / height);
        float width = style == 20 ? 5.0 : 8.0;
        if (mod(p.y, height) < 1.0 || mod(p.x + mod(row, 2.0) * width * 0.5, width) < 1.0) detail *= style == 4 ? 0.65 : 0.72;
        detail *= 0.87 + grain(vec3(floor(p.x / width), row, 9.0)) * 0.22;
    } else if (style == 5) {
        if (face_normal.y > 0.5) detail *= mod(p.x, 4.0) < 2.0 ? 0.61 : 1.15;
    } else if (style == 6 || style == 7) {
        bool border = p.x < 2.0 || p.x > 13.0 || p.y < 2.0 || p.y > 13.0;
        detail *= border ? 1.23 : 0.77;
        if (!border && (p.x == 3.0 || p.x == 12.0 || p.y == 3.0 || p.y == 12.0)) detail *= 0.6;
        if (style == 7 && p.x >= 4.0 && p.x <= 11.0 && p.y >= 5.0 && p.y <= 11.0) {
            base = vec3(0.48, 0.63, 0.64) * shade;
            if (p.x == 7.0 || p.x == 8.0 || p.y == 8.0) {base = tint.rgb;detail *= 1.25;}
        }
        if (style == 6 && p.x == 12.0 && p.y >= 8.0 && p.y <= 9.0) {base = vec3(0.86, 0.78, 0.52) * shade;detail = 1.0;}
    } else if (style >= 8 && style <= 10) {
        vec3 mineral = style == 8 ? vec3(0.12, 0.15, 0.17) : (style == 9 ? vec3(0.74, 0.49, 0.34) : vec3(0.30, 0.85, 0.91));
        base = vec3(0.53, 0.56, 0.56) * shade;
        if (grain(vec3(floor(p / 2.0), 7.0)) > 0.68) base = mineral * shade;
    } else if (style == 13 || style == 30) {
        if (mod(p.x, 8.0) == 0.0 || mod(p.y, 4.0) == 0.0) detail *= 0.55;
        if (style == 13 && p.x >= 3.0 && p.x <= 12.0 && p.y >= 2.0 && p.y <= 8.0) {base = vec3(0.10, 0.12, 0.13) * shade;detail = 1.0;}
    } else if (style == 15) {
        if (face_normal.y > 0.5 && p.y > 11.0) base = vec3(0.86, 0.86, 0.78) * shade;
        if (face_normal.y < 0.5 && p.y < 6.0) base = vec3(0.42, 0.30, 0.20) * shade;
    } else if (style == 16 || style == 17) {
        bool frame = p.x < 3.0 || p.x > 12.0 || p.y < 3.0 || p.y > 12.0;
        detail *= frame ? 0.43 : 1.08;
        if (!frame) EMISSION = tint.rgb * (style == 17 ? 0.55 : 0.38);
    } else if (style == 21) {
        detail = 0.92 + noise * 0.12;
    } else if (style == 22 || style == 31) {
        detail = 0.94 + noise * 0.07;
        if (style == 31 && mod(p.x + p.y, 13.0) < 1.0) detail *= 1.12;
    } else if (style == 23) {
        detail *= grain(vec3(floor(p / 3.0), 11.0)) > 0.5 ? 0.80 : 1.07;
    } else if (style == 24) {
        detail *= mod(p.x, 4.0) < 1.0 ? 0.63 : 1.02;
        if (p.x == 6.0 && mod(p.y, 5.0) == 2.0) base *= 0.43;
    } else if (style == 25 || style == 26 || style == 27) {
        if (style == 25 && mod(p.x + floor(p.y / 3.0), 15.0) < 1.0) detail *= 0.73;
        if (style == 26 && (p.x == 1.0 || p.x == 14.0) && (p.y == 1.0 || p.y == 14.0)) detail *= 0.60;
        if (style == 27 && (mod(p.x, 8.0) < 1.0 || mod(p.y, 8.0) < 1.0)) detail *= 0.62;
    } else if (style == 28) {
        detail *= mod(p.x + p.y, 2.0) < 1.0 ? 0.90 : 1.03;
    }
    if (style == 34) {
        bool mark = mod(p.y, 6.0) < 1.0 && mod(p.x + floor(p.y / 6.0) * 3.0, 9.0) < 5.0;
        detail *= mark ? 0.25 : 1.03;
    }
    vec3 sky_light = mix(vec3(0.16, 0.21, 0.34), vec3(1.0, 0.98, 0.94), daylight) * (0.025 + daylight * 0.975) * pow(UV.x, 1.6);
    vec3 torch_light = vec3(1.0, 0.70, 0.36) * pow(UV.y, 1.8) * 1.45;
    vec3 illumination = max(vec3(0.0025), max(sky_light, torch_light));
    ALBEDO = base * detail;
    EMISSION += ALBEDO * illumination;
    if (style == 33) {
        bool flame = fract(world_position.y) > 0.46;
        ALBEDO = flame ? vec3(1.0, 0.62, 0.16) : vec3(0.42, 0.25, 0.10) * detail;
        EMISSION = flame ? vec3(1.0, 0.43, 0.07) * 1.6 : vec3(0.0);
    }
    if (style == 16) EMISSION = base * 0.3;
    ROUGHNESS = 0.92;
}
void light() {
    // Daylight is already occluded by the voxel field. Point lights contribute
    // realtime illumination with native terrain shadows for held torches.
    if (!LIGHT_IS_DIRECTIONAL) DIFFUSE_LIGHT += max(dot(NORMAL, LIGHT), 0.0) * ATTENUATION * LIGHT_COLOR / PI;
}
