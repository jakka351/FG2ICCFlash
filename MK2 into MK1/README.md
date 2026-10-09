# AR79-14D017-JL.phf Modified FDIM Firmware
***For installs where MK2 FDIM going into MK1, this patched firmware provides CANbus compatibility***
  
There are three modifications to this firmware from the original   
  - Dimming from `0x128` is inverted to match the MK1 CANbus  
  - Ambient Temperature grabbed from `0x353` is now rebroadcast on `0x313` `Byte 0`
  - HVAC Buttons on `0x307` are now aligned with what the MK1 expects
