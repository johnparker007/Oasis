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
typedef struct FabricCharacterDisplay { uint32_t struct_size, struct_version; char identifier[ID]; uint32_t character_count, character_capacity, characters[CHARS]; uint8_t attributes[CHARS]; float brightness; } FabricCharacterDisplay;
typedef struct FabricSegmentDisplay { uint32_t struct_size, struct_version; char identifier[ID]; uint32_t digit_count, digit_capacity; uint64_t segment_masks[DIGITS]; } FabricSegmentDisplay;
typedef struct FabricDotMatrixDisplay { uint32_t struct_size, struct_version; char identifier[ID]; uint32_t width, height, dot_count, dot_capacity; uint8_t dots[2048]; float brightness; } FabricDotMatrixDisplay;
typedef struct FabricMachineSnapshot { uint32_t struct_size, struct_version; uint64_t sequence; FabricLamp *lamps; uint32_t lamp_capacity, lamp_count; FabricReel *reels; uint32_t reel_capacity, reel_count; FabricCharacterDisplay *character_displays; uint32_t character_display_capacity, character_display_count; FabricSegmentDisplay *segment_displays; uint32_t segment_display_capacity, segment_display_count; FabricDotMatrixDisplay *dot_matrix_displays; uint32_t dot_matrix_display_capacity, dot_matrix_display_count; } FabricMachineSnapshot;
typedef struct FabricAudioFormat { uint32_t struct_size, struct_version, sample_rate; uint16_t channel_count, bits_per_sample; uint8_t interleaved, signed_samples, little_endian, reserved; } FabricAudioFormat;
typedef struct AmberReelConfigV1 { uint32_t reel_index, enabled, steps, opto_start, opto_end, opto_invert; } AmberReelConfigV1;
typedef struct AmberReelConfigurationV1 { uint32_t struct_size, version, reel_count, apply_mask; AmberReelConfigV1 reels[8]; } AmberReelConfigurationV1;
typedef struct AmberCoinChannelConfigV1 { uint32_t channel_index, enabled, value, lockout_invert, reserved; } AmberCoinChannelConfigV1;
typedef struct AmberCoinRouteConfigV1 { uint32_t route_index, enabled, counter_in, counter_out, port_index, coin_code, level, full_level; } AmberCoinRouteConfigV1;
typedef struct AmberCoinConfigurationV1 { uint32_t struct_size, version, channel_apply_mask, route_apply_mask; AmberCoinChannelConfigV1 channels[6]; AmberCoinRouteConfigV1 routes[8]; uint32_t lockout_port_base, lockout_port_value, configuration_flags, reserved; } AmberCoinConfigurationV1;
typedef struct FabricAmberConfigurationV1 { uint32_t magic, struct_size, version, flags; AmberReelConfigurationV1 reels; AmberCoinConfigurationV1 coins; uint32_t percentage_switch, reserved[3]; } FabricAmberConfigurationV1;
typedef struct FabricAmberMpu5ReelConfigV1 { uint32_t reel_index,steps,opto_start,opto_end,opto_invert; } FabricAmberMpu5ReelConfigV1;
typedef struct FabricAmberMpu5ReelConfigurationV1 { uint32_t struct_size,version,reel_count,apply_mask; FabricAmberMpu5ReelConfigV1 reels[8]; } FabricAmberMpu5ReelConfigurationV1;
typedef struct FabricAmberMpu5CoinChannelConfigV1 { uint32_t channel_index,enabled,value,lockout_invert,reserved; } FabricAmberMpu5CoinChannelConfigV1;
typedef struct FabricAmberMpu5CoinConfigurationV1 { uint32_t struct_size,version,channel_count,apply_mask,communication_style,communication_invert,pulse_cycles,edc_enabled; FabricAmberMpu5CoinChannelConfigV1 channels[6]; } FabricAmberMpu5CoinConfigurationV1;
typedef struct FabricAmberMpu5OptionsV1 { uint32_t struct_size,version,apply_mask,dip_switch_bits,stake,prize,percentage,characteriser_address,pic_mode,sec_fitted,hopper_type,reel_jumper_profile_0,reel_jumper_profile_1,reserved[2]; } FabricAmberMpu5OptionsV1;
typedef struct FabricAmberMpu5ConfigurationV1 { uint32_t magic,struct_size,version,flags; FabricAmberMpu5ReelConfigurationV1 reels; FabricAmberMpu5CoinConfigurationV1 coins; FabricAmberMpu5OptionsV1 options; } FabricAmberMpu5ConfigurationV1;

#define SIZE(T) printf("sizeof.%s=%zu\n", #T, sizeof(T))
#define OFF(T,F) printf("offsetof.%s.%s=%zu\n", #T, #F, offsetof(T,F))
int main(void) {
    SIZE(FabricLaunchRequest); SIZE(FabricRomResource); SIZE(FabricCapabilities); SIZE(FabricInput);
    SIZE(FabricLamp); SIZE(FabricReel); SIZE(FabricCharacterDisplay); SIZE(FabricSegmentDisplay);
    SIZE(FabricDotMatrixDisplay); SIZE(FabricMachineSnapshot); SIZE(FabricAudioFormat); SIZE(AmberReelConfigV1);
    SIZE(AmberReelConfigurationV1); SIZE(AmberCoinChannelConfigV1); SIZE(AmberCoinRouteConfigV1);
    SIZE(AmberCoinConfigurationV1); SIZE(FabricAmberConfigurationV1);
    SIZE(FabricAmberMpu5ReelConfigV1); SIZE(FabricAmberMpu5ReelConfigurationV1); SIZE(FabricAmberMpu5CoinChannelConfigV1); SIZE(FabricAmberMpu5CoinConfigurationV1); SIZE(FabricAmberMpu5OptionsV1); SIZE(FabricAmberMpu5ConfigurationV1);
    OFF(FabricLaunchRequest, rom_paths); OFF(FabricLaunchRequest, machine_configuration);
    OFF(FabricLaunchRequest, rom_resources); OFF(FabricRomResource, path);
    OFF(FabricMachineSnapshot, lamps); OFF(FabricMachineSnapshot, reels);
    OFF(FabricMachineSnapshot, character_displays); OFF(FabricMachineSnapshot, segment_displays);
    OFF(FabricMachineSnapshot, dot_matrix_displays); OFF(FabricMachineSnapshot, dot_matrix_display_capacity); OFF(FabricMachineSnapshot, dot_matrix_display_count);
    OFF(FabricCharacterDisplay, brightness);
    OFF(FabricAmberMpu5ConfigurationV1,reels); OFF(FabricAmberMpu5ConfigurationV1,coins); OFF(FabricAmberMpu5ConfigurationV1,options);
    return 0;
}
