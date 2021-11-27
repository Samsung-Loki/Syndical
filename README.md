![badges](https://img.shields.io/github/contributors/TheAirBlow/Syndical.svg)
![badges](https://img.shields.io/github/forks/TheAirBlow/Syndical.svg)
![badges](https://img.shields.io/github/stars/TheAirBlow/Syndical.svg)
![badges](https://img.shields.io/github/issues/TheAirBlow/Syndical.svg)
![badges](https://github.com/TheAirBlow/Syndical/actions/workflows/build.yml/badge.svg)
[![Codacy Badge](https://app.codacy.com/project/badge/Grade/4cf0cbd38c014349a3612c43711279ce)](https://www.codacy.com/gh/TheAirBlow/Syndical/dashboard?utm_source=github.com&amp;utm_medium=referral&amp;utm_content=TheAirBlow/Syndical&amp;utm_campaign=Badge_Grade)
# Syndical
This is an alternative to [SamLoader](https://github.com/nlscc/samloader) - cleaner code, easier to understand and tamper with. \
Written purely in C#. [SamLoader](https://github.com/nlscc/samloader) was, of course, the base of Syndical, but impemented differently. \
Current progress can be viewed here: [Trello board](https://trello.com/b/3kekg3El/syndical).

## How to use
### Arguments
```
  -m, --mode                Required. Which mode I should use
  -V, --encrypt-version     (Default: V4) Encryption method version
  -v, --firmware-version     Firmware version
  -i, --input               File to decrypt
  -o, --output              Filename for decrypted/downloaded file
  -M, --model               Required. Device model
  -r, --region              Required. Device region
  -f, --factory             Download factory firmware (BINARY_NATURE = 1)
  --help                    Display this help screen.
  --version                 Display version information.
```
### Modes
```
Download     Download firmware
Decrypt      Decrypt firmware
Fetch        Fetch latest firmware
```
### Examples
Download: 
```
./Syndical.Application -m Download -v A207FXXU2CUI2/A207FOXM2CUI2/A207FXXU2CUI2/A207FXXU2CUI2 -M SM-A207F -r SER -f
```
Decrypt: 
```
./Syndical.Application -m Decrypt -v A207FXXU2CUI2/A207FOXM2CUI2/A207FXXU2CUI2/A207FXXU2CUI2 -M SM-A207F -r SER -f
```
Fetch device firmware list: 
```
./Syndical.Application -m Fetch -M SM-A207F -r SER
```
If `--factory` is present, `BINARY_NATURE` is set to 1 instead of 0. 

## Credits
[TheAirBlow](https://github.com/theairblow) for Syndical itself \
[nlscc](https://github.com/nlscc) for SamLoader

## Licence
[Mozilla Public License Version 2.0](https://github.com/TheAirBlow/Syndical/blob/main/LICENCE)
