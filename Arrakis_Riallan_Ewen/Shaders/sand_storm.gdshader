shader_type fog;

uniform sampler3D base_noise_tex : repeat_enable;
uniform sampler3D detail_noise_tex : repeat_enable;

uniform float base_noise_scale = 0.05;
uniform float detail_noise_scale = 0.15;

uniform vec3 wind_direction = vec3(1.0, 0.0, 0.2);
uniform float wind_speed = 1.0;

uniform vec3 albedo : source_color = vec3(0.65, 0.45, 0.25);
uniform float density_multiplier = 8.0;
uniform float edge_softness = 2.0;

void fog() {
    vec3 world_pos = WORLD_POSITION;
    
    vec3 base_uvw = (world_pos * base_noise_scale) + (wind_direction * TIME * wind_speed);
    vec3 detail_uvw = (world_pos * detail_noise_scale) + (wind_direction * TIME * wind_speed * 1.2);
    
    float base_noise = texture(base_noise_tex, base_uvw).r;
    float detail_noise = texture(detail_noise_tex, detail_uvw).r;
    
    float combined_noise = clamp(base_noise - (detail_noise * 0.4), 0.0, 1.0);

    float fluffy_noise = smoothstep(0.25, 0.65, combined_noise);
    
    float dist_to_edge = -SDF;
    float edge_fade = smoothstep(0.0, edge_softness, dist_to_edge - (fluffy_noise * edge_softness));
    
    DENSITY = fluffy_noise * edge_fade * density_multiplier;
    ALBEDO = albedo;
}