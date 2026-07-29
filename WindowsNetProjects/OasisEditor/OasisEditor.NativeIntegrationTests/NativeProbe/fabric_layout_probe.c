#include <stddef.h>
#include <stdint.h>
#include <stdio.h>

#define ID 64
#define PATH 1024
#define CHARS 16
#define DIGITS 16

typedef struct FabricRomResource { uint32_t struct_size, struct_version, role, slot; const char *path; uint64_t reserved[2]; } FabricRomResource;
typedef struct FabricLaunchRequest { uint32_t struct_size, struct_version; char backend_kind[ID], machine_identifier[ID], backend_path[PATH]; const char *const *rom_paths; uint32_t rom_path_count; const void *machine_configuration; uint32_t machine_configuration_size, reserved; const FabricRomResource *rom_resources; uint32_t rom_resource_count; } FabricLaunchRequest;
typedef struct FabricCapabilities { uint32_t struct_size, struct_version; uint64_t flags, reserved[4]; } FabricCapabilities;
typedef struct FabricInput { uint32_t struct_size, struct_version; char identifier[ID]; int32_t numerical_index; uint8_t active, reserved[7]; } FabricInput;
typedef struct FabricLamp { uint32_t struct_size, struct_version; char identifier[ID]; int32_t numerical_index; uint8_t logical_state, reserved[3]; float brightness; } FabricLamp;
typedef struct FabricReel { uint32_t struct_size, struct_version; char identifier[ID]; int32_t numerical_index, position; } FabricReel;
typedef struct FabricCharacterDisplay { uint32_t struct_size, struct_version; char identifier[ID]; uint32_t character_count, character_capacity, characters[CHARS]; uint8_t attributes[CHARS]; } FabricCharacterDisplay;
typedef struct FabricSegmentDisplay { uint32_t struct_size, struct_version; char identifier[ID]; uint32_t digit_count, digit_capacity; uint64_t segment_masks[DIGITS]; } FabricSegmentDisplay;
typedef struct FabricMachineSnapshot { uint32_t struct_size, struct_version; uint64_t sequence; FabricLamp *lamps; uint32_t lamp_capacity, lamp_count; FabricReel *reels; uint32_t reel_capacity, reel_count; FabricCharacterDisplay *character_displays; uint32_t character_display_capacity, character_display_count; FabricSegmentDisplay *segment_displays; uint32_t segment_display_capacity, segment_display_count; } FabricMachineSnapshot;
typedef struct FabricAudioFormat { uint32_t struct_size, struct_version, sample_rate; uint16_t channel_count, bits_per_sample; uint8_t interleaved, signed_samples, little_endian, reserved; } FabricAudioFormat;
typedef struct AmberReelConfigV1 { uint32_t reel_index, enabled, steps, opto_start, opto_end, opto_invert; } AmberReelConfigV1;
typedef struct AmberReelConfigurationV1 { uint32_t struct_size, version, reel_count, apply_mask; AmberReelConfigV1 reels[8]; } AmberReelConfigurationV1;
typedef struct AmberCoinChannelConfigV1 { uint32_t channel_index, enabled, value, lockout_invert, reserved; } AmberCoinChannelConfigV1;
typedef struct AmberCoinRouteConfigV1 { uint32_t route_index, enabled, counter_in, counter_out, port_index, coin_code, level, full_level; } AmberCoinRouteConfigV1;
typedef struct AmberCoinConfigurationV1 { uint32_t struct_size, version, channel_apply_mask, route_apply_mask; AmberCoinChannelConfigV1 channels[6]; AmberCoinRouteConfigV1 routes[8]; uint32_t lockout_port_base, lockout_port_value, configuration_flags, reserved; } AmberCoinConfigurationV1;
typedef struct FabricAmberConfigurationV1 { uint32_t magic, struct_size, version, flags; AmberReelConfigurationV1 reels; AmberCoinConfigurationV1 coins; uint32_t percentage_switch, reserved[3]; } FabricAmberConfigurationV1;

#define SIZE(T) printf("sizeof.%s=%zu\n", #T, sizeof(T))
#define OFF(T,F) printf("offsetof.%s.%s=%zu\n", #T, #F, offsetof(T,F))
int main(void) {
    SIZE(FabricLaunchRequest); SIZE(FabricRomResource); SIZE(FabricCapabilities); SIZE(FabricInput);
    SIZE(FabricLamp); SIZE(FabricReel); SIZE(FabricCharacterDisplay); SIZE(FabricSegmentDisplay);
    SIZE(FabricMachineSnapshot); SIZE(FabricAudioFormat); SIZE(AmberReelConfigV1);
    SIZE(AmberReelConfigurationV1); SIZE(AmberCoinChannelConfigV1); SIZE(AmberCoinRouteConfigV1);
    SIZE(AmberCoinConfigurationV1); SIZE(FabricAmberConfigurationV1);
    OFF(FabricLaunchRequest, rom_paths); OFF(FabricLaunchRequest, machine_configuration);
    OFF(FabricLaunchRequest, rom_resources); OFF(FabricRomResource, path);
    OFF(FabricMachineSnapshot, lamps); OFF(FabricMachineSnapshot, reels);
    OFF(FabricMachineSnapshot, character_displays); OFF(FabricMachineSnapshot, segment_displays);
    return 0;
}
