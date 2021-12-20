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

## OSS Licence
We use a free OSS licence from JetBrains to develop Hreidmar. \
You can apply to get one [here](https://jb.gg/OpenSourceSupport)

## Why I should use Syndical?
1) It looks a lot better than any other firmware downloaders
2) Download & Decrypt is a lot faster, as it won't download the entire file and then only decrypt it.
3) Because I'll steal your liver 

## Warning
Resume mode might work not as expected. \
Also Samsung ships only two last firmware versions, Fetch is proof.

## Screenshots
### Download mode
![Screenshot_20211127_201356](https://user-images.githubusercontent.com/68467762/143686936-bea4fc37-76ba-4050-a7dc-89dda131abeb.png)
### Fetch mode
![Screenshot_20211127_201618](https://user-images.githubusercontent.com/68467762/143686992-f2bcc648-7538-44eb-acb0-c4f1ba5e5446.png)
### Decrypt mode
![Screenshot_20211128_145859](https://user-images.githubusercontent.com/68467762/143763448-214d8ff9-05d1-497f-bc46-13cc8ffd5b7b.png)
### Download & Decrypt mode
![Screenshot_20211128_145753](https://user-images.githubusercontent.com/68467762/143763417-260fc681-dca5-4fb4-9252-527e780ecfd7.png)

## How to use
### Arguments
```
  -m, --mode                  Required. Which mode I should use
  -v, --firmware-version       Firmware version
  -i, --input                 File to decrypt
  -o, --output                Filename for decrypted/downloaded file
  -M, --model                 Required. Device model
  -r, --region                Required. Device region
  -f, --factory               Download factory firmware (Binary Nature)
  -h, --disable-hash-check    Disables hash check in Download mode
  -r, --disable-resume        Disables resume in Download mode
  --help                      Display this help screen.
  --version                   Display version information.
```
### Modes
```
Download          Download firmware
Decrypt           Decrypt firmware
Fetch             Fetch latest firmware
DownloadDecrypt   Download and decrypt firmware simultaneously
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
Download & Decrypt: 
```
./Syndical.Application -m DownloadDecrypt -v A207FXXU2CUI2/A207FOXM2CUI2/A207FXXU2CUI2/A207FXXU2CUI2 -M SM-A207F -r SER -f
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
