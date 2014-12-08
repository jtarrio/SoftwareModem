# Software Modem

An implementation of the ITU-T V.21 modem standard, working on a PC over a sound card.

This program uses your computer's sound card to communicate using the V.21 standard, which allows for communications at up to 300 bps. It can actually talk to a modem, both in call and answer mode, using a small electronic circuit that connects them.

This program was written in C# on Visual Studio Community 2013, with the NAudio library.

## Hardware

If you'd like to connect two computers using this program, you only need a couple of audio cables: connect the line input from one computer to the line output of the other computer and vice versa. Theoretically you could also connect two copies of the program running on the same computer by connecting the computer's line out with its line in using a single audio cable. I haven't tried it, so YMMV.

You can also connect the computer's sound card to an actual modem using a small electronic circuit that simulates a phone line. You are going to need:

1. A 9-volt battery.
2. A 9-volt battery connector.
2. A 470-Ohm resistor.
3. A 150-Ohm resistor.
4. A 600/600 Ohm transformer. One of the windings must have a center tap. You can scavenge one from an old modem or telephone, or buy one (I used the Xicon 42TL016-RC, but there are many in the market).
5. A length of phone cable with an RJ11 modular connector.
6. Two 3.5 mm audio jacks.
7. A modem.
8. (Recommended) A corded phone

You can use a higher voltage battery if you want, but make sure to scale the 470-Ohm resistor up as well so that the current is limited to some 20-25 mA.

**WARNING**: This circuit is designed to simulate a phone line so you can connect a modem or telephone directly to your computer. It is not designed to connect the computer to a phone line. This circuit uses 9 volt DC, while phone lines may present up to 100 volts AC, which will then go into your sound card. This means that **you will burn your computer if you connect it to the phone line using this circuit**.

![Line simulator diagram](diagram-modem.png)

Start by connecting the components as in the diagram. You may use a breadboard, a PCB, or even solder them "dead bug style". On the modem side, connect the battery and the resistor to the transformer's outer taps, and connect the phone cable to the battery and resistor. On the other side, connect the resistor to the center tap and connect the other end of the resistor to both jacks' sleeves. Then connect an outer tap to one of the jack's tip, and do the same for the other outer tap and jack.

Then connect one of the jacks to your sound card's "line in" socket, and the other jack to the "line out" or "headphone" socket. Both jacks are connected in the same way, so it's indifferent which one is connected where.

To test the circuit you can plug a corded telephone to the RJ11 connector, and try playing and recording sounds over the headset.

To use the circuit, just replace the telephone with a modem (or, if you have two RJ11 connectors in parallel, leave the telephone connected and plug the other connector into the modem).

## How to use

After you start the program, select the correct line in device in the drop down box.

If you are using a modem on the remote side, configure the terminal emulator with 8 data bits, no parity, and 1 stop bit (8N1).

### Make a call

On the Software Modem window, press the "Call" button. On the remote side, press the "Answer" button or type "ATA" if you are using a modem with a terminal emulator. Wait a few seconds until the terminal emulator displays "CONNECT".

### Receive a call

On the remote side, press the "Call" button or type "ATD" if you are using a modem with a terminal emulator, and then press "Answer" on the Software Modem window. Wait a few seconds until the terminal emulator displays "CONNECT".

### Terminate a call

Press the "Hang up" button on the Software Modem window. On the remote side, press the "Hang up" button or, if you are using a modem with a terminal emulator, type "+++" without pressing Return, wait for "OK" to be displayed, and then type "ATH".

