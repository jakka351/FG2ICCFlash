<a href="https://testerpresent.com.au"><img width="18%" height="18%" align="right" alt="RightToRepair" src="https://github.com/user-attachments/assets/b41c7c2a-f293-45aa-97d7-bebcd65e9433" /></a>
# FG2ICCFlash
### *This project is dedicated to Janis*  

<img width="30%" height="30%" alt="351" src="https://github.com/user-attachments/assets/90f7a269-0958-4e4b-9c02-faf627f02efc" />

  
## What this is
This is full spec engineering tool for use with the Ford FGII Falcon's Front Display Interface Module, aka the ICC, the Interior Command Centre. It will perform diagnostics and reprogramming of the controller
via the CANbus(yes the OBD port) using an SAE J2534 PassThru Device. The hope is that while currently these units are able to have the firmware updated via USB, providing an OBD option for people to try may
help to extend the overall life that we get out of these units across the board. I am simply trying to increase the average longevity of these things. 

<img width="854" height="746" alt="image" src="https://github.com/user-attachments/assets/aa896c8d-3e71-420d-9553-a9d23c901ce5" />

  
## Instructions for use
***This software has not yet been tested on an FDIM***   
Download the software from the releases page, and fire it up, plug in your J2534 device to USB and OBD, select it in the software, select your firmware and flash! Simples. The software may be 
a bit daunting, but I have tried to keep it as closely aligned to the engineering specification as possible. **If you come across a problem or a fault or a bug, please raise an issue in the repo issues section.**  
  
## Recore - Reimplemented 
The USB Recore functionality has been implemented into this software as well, alongside both the On Demand Self Test and the EOL Assembly Self Test.  

## Firmware
A collection of firmware files have been provided for use with the software in PHF, Binary and Hex format, use as you will. I believe the -CS firmware is the FPV spec firmware, with extra EEPROM space. 

## Brick risk  
These Mark 2 FDIM's are extremely delicate and as such any use of this software may inexplicably brick the FDIM rendering it essentially useless. Use at your own risk, if you are
unsure, contract the services of a suitably qualified <a href="https://barrascan.net/">Module Programmer</a> to program the module. You can thank SWSA Australia for this. 

## FFAU  
<a href="https://fordforums.com.au"><img width="680" height="118" alt="FFAU" src="https://github.com/user-attachments/assets/c430872d-e4a5-4986-8bb8-50aa7657f5d9" /></a>  

Failing ICC Thread: https://fordforums.com.au/showthread.php?t=11479908&page=2   
  

<img width="20%" height="20%" alt="Get your anonsies" align="right" src="https://github.com/user-attachments/assets/084977ea-9fcb-4995-8fde-ceaa79152bd0" />

## Open Sauce
*Engineering Specification documentation regarding this controller was sourced via a guerilla diagnosticos group that reverse engineer without fear for the Right to Repair!*  

