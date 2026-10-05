<a href="https://testerpresent.com.au"><img width="24%" height="24%" align="right" alt="RightToRepair" src="https://github.com/user-attachments/assets/b41c7c2a-f293-45aa-97d7-bebcd65e9433" /></a>
# FG2ICCFlash

<img width="55%" height="55%" alt="351" align="center" src="https://github.com/user-attachments/assets/90f7a269-0958-4e4b-9c02-faf627f02efc" />


<img width="25%" height="25%" alt="aus" align="right" src="https://github.com/user-attachments/assets/157d6280-f6bf-4c5e-9a6a-32721e624b77" />

## What this is
This is a full spec engineering tool for use with the Ford MkII <a href="https://www.drive.com.au/news/ford-fg-falcon-new-names-new-models-20080216-1438u/">FG Falcon</a>'s Front Display Interface Module, aka the ICC, the Interior Command Centre. It **replaces the Ford Dealership firmware reprogramming system**,
which is done via the Ford IDS software, which is broken for this procedure, and there is no chance in hell that Bosch Automotive Service Solutions(who are the actual maintainers of Ford IDS) will ever touch this
functionality ever again, so here is a ***fully open source and substantive solution*** for performing Module Programming and Configuration and Firmware Recore on the MK2 ICC.   
  
The reprogramming happens in two stages, the first via the CANbus(yes the OBD port) using an SAE J2534 PassThru Device, the second is via USB. The intent here is in the spirit of Right to Repair, and the project's mission is to help to extend the overall life that we get out of these units across the board. This project is aimed at the **DIY folks at home, Independent Workshops and Ford Dealerships** who need the functionality that they have lost to time back in a working state again. Also in addition the USB-Recore functionality is built out and a small linux scripting engine for developing custom payloads to recore, as well as community based firmware backups are selectable as a recore option, and navimaps.zip as well. Enjoy, hopefully this helps some folks save a few units from the bin. 

 ***We have a bench verified succesful OBD flash!***    
<img width="75%" height="75%" alt="image" src="https://github.com/user-attachments/assets/ef161f27-faf3-4c71-ab3c-c5b73002f76a" />
  
 ***We have a bench verified succesful USB flash!***    
<img width="75%" height="75%" alt="image" src="https://github.com/user-attachments/assets/ab5e54d3-f15f-492c-a840-600952e7fc76" />


## Requirements
  - A windows laptop with .NET 4.8.1 x86 installed.
  - An SAE <a hef="https://www.boschdiagnostics.com/j2534-faq">J2534 PassThru</a> Device with Ford MidSpeed CAN @ 125KBPS capability. <a href="https://obdxpro.com/product/obdx-pro-fx-ford-obd2-j2534-diagnostics-and-tuning/">Here</a>, and <a href="https://www.tiperformance.com.au/products/obdx-pro-ft-ford-j2534-diagnostic-and-tuning-cable/?srsltid=AU7gw4ViX67wHhJb-DVhP5L0pVIaLImWDDanHMrMYQyuglbJXWjkRlaI">here</a>  
  - A USB drive with at least 8 gigabytes of memory
  - The ability to <a href="https://testerpresent.com.au/FaultFinding/Superpower.php">fault find</a> and troubleshoot will come in very handy, as will a can-do attitude - **a DIY automotive noob IS capable of using this software**

  
## Instructions for use

Download the software from the releases page, and fire it up, plug in your J2534 device to USB and OBD, select it in the software, select your firmware and flash! Stage 2 via USB is explained within the tool. 
The software may be a bit daunting, but I have tried to keep it as user friendly as possible. **If you come across a problem or a fault or a bug, please raise an issue in the repo issues section.**  
<img width="75%" height="75%" alt="image" src="https://github.com/user-attachments/assets/c8c3a6d1-c3da-4551-a0a5-d2ef22e83c11" />

## Diagnostics
Read and Clear DTC is available from the diagnostics tab, alongside both the On Demand Self Test and the EOL Assembly Self Test.  

## Configuration via EEPROM/As Built Data
There is a built in As Built Data editor in the tool, that allows you to set the module configuration options, programmed VIN and single/dual zone settings. This is currently untested. The tool will
also load and save .ABT As Built files in the forscan format. 

## Firmware
A collection of firmware files have been provided for use with the software in PHF, Binary and Hex format, use as you will. I believe the -CS firmware is the FPV spec firmware, with extra EEPROM space. 
Official Firmware, Updates and Community pulled recore backups are available for download from within the Stage 2 tab of the tool, and are hosted at https://www.testerpresent.com.au/FDIM/

## Brick risk  
These Mark 2 FDIM's are extremely delicate and as such any use of this software may inexplicably brick the FDIM rendering it essentially useless. Use at your own risk, if you are
unsure, contract the services of a suitably qualified <a href="https://barrascan.net/">Module Programmer</a> to program the module. 
<a href="https://www.sws.co.jp/en/corporation/outline/office/detail/sws_australia_pty_ltdsws-a.html">You can thank SWSA for that.</a> 

## FFAU  
<a href="https://fordforums.com.au"><img width="680" height="118" alt="FFAU" src="https://github.com/user-attachments/assets/c430872d-e4a5-4986-8bb8-50aa7657f5d9" /></a>  

Failing ICC Thread: https://fordforums.com.au/showthread.php?t=11479908&page=2   

## Thanks
**JasonACT** for the extensive documentation and development effort put in over a long period of time.   
  

<img width="20%" height="20%" alt="Get your anonsies" align="right" src="https://github.com/user-attachments/assets/084977ea-9fcb-4995-8fde-ceaa79152bd0" />

## Open Sauce
*Engineering Specification documentation regarding this controller was sourced via a guerilla diagnosticos group that reverse engineer without fear for the Right to Repair!*  

