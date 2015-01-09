# MAC Address Tool

A small tool allowing Windows users to easily change the MAC-address of network adapters through a registry key.

![Screenshot](/screenshot.png?raw=true)

## Disclaimer
**Use at your own risk!** I have tested this tool only on my own system. I do not know what the effect of modifying the registry key will be be on other systems, under other circumstances.

## How does it work?
For some network interfaces the MAC-address (also called "NetworkAddress" or "Physical Address" at various places in Windows) can be changed by going to the properties of the adapter, selecting the Advanced tab, and changing the value of "NetworkAddress". This sets a value somewhere in the registry (namely, `HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\XXXX\NetworkAddress`, where `XXXX` are four digits depending on the adapter).
However, sometimes the "NetworkAddress" is not present in the properties of the adapter, while setting the corresponding value in the registry still has effect. This tool looks up your network adapter in the registry, and sets the "NetworkAddress" to the value of your choice.

When starting, the app asks for elevated permissions in order to be able to edit the registry.

## MAC addresses
There seem to be two kinds of MAC addresses:

*  Actual addresses assigned to network adapters when they were manufactured,
* Locally administered addresses, assigned to a network adapter by the OS or the user.

The second-least-significant bit of the first byte of locally administered addresses must be 1, while the least-significant must always be 0. Thus, the last two bits of the first byte of any MAC address chosen by the user must be 10. When displaying the address in hexadecimal form, this means that **the second character must be 2, 5, A or E.**

Setting the NetworkAddress value in the registry to an address that does not satisfy this requirement has no effect. Therefore, the app only allows the user to set a MAC address that satisfies this requirement.
