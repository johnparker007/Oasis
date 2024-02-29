This folder contains the effect wav files
-----------------------------------------

The following effect file names are supported and are selected via the Effects Tab in Config:

General Effects
---------------

2pSlide.wav                 - Played when 2p payout solenoid is activated
10pSlide.wav                - Played when 10p payout solenoid is activated
20pSlide.wav                - Played when 20p payout solenoid is activated
50pSlide.wav                - Played when 50p payout solenoid is activated 
PoundSlide.wav              - Played when £1  payout solenoid is activated 
TokenSlide.wav              - Played when 10p/20p token payout solenoid is activated
MiscSlide.wav               - Played when £2.50 (5 x 50p) Slide is activated

Meter.wav                   - Played when a EM Meter is activated

ButtonDown.wav              - Played when a layout button is pressed
ButtonUp.wav                - Played when a layout button is released

HopperMotor1.wav            - Played when Hopper Motor 1 is switched on
HopperCoin1.wav             - Played when a coin is payed out from Hopper 1
HopperMotor2.wav            - Played when Hopper Motor 2 is switched on
HopperCoin2.wav             - Played when a coin is payed out from Hopper 2

NoteIn.wav                  - Played when a note is inserted into a Note Acceptor
NoteInReject.wav            - Played when a note is rejected by the Note Acceptor
NoteInStack.wav             - Played when a note is accepted and sent to the stacker

CoinIn.wav                  - Played when a coin is inserted into a roll down electronic coin mech
CoinInSwitch.wav            - Played when a coin is accepted by the mech
CoinInRejected.wav          - Played when a coin is rejected by the mech and drops down to the payout tray

CoinInToken.wav             - Played when a token is inserted into a roll down electronic coin mech
CoinInTokenSwitch.wav       - Played when a token is accepted by the mech
CoinInTokenRejected.wav     - Played when a token is rejected by the mech and drops down to the payout tray

CoinInS10.wav               - Played when a coin is inserted in a S1/S10 Type mech
CoinInS10Rejected.wav       - Played when a coin is rejected from a S10 Type mech
CoinInS10Switch.wav         - Played when a token is accepted by a S1/S10 Type mech

CoinInS10Token.wav          - Played when a token is inserted in a S10 Token Type mech
CoinInS10TokenRejected.wav  - Played when a token is rejected from a S10 Token Type mech
CoinInS10TokenSwitch.wav    - Played when a token is accepted by a S10 Token Type mech

CoinInDrop.wav              - Played when a coin is inserted a Comparitor style mech
CoinDropReject.wav          - Played when a coin is rejected from a Comparitor style mech

S1Locked.wav                - Played when trying to insert a coin into a S1 Mech that is locked out

LockoutOn.wav               - Played when a lockout solenoid is activated
LockoutOff.wav              - Played when a lockout solenoid is deactivated

Stepper.wav                 - Played when a SRU/SYS80/MPS2 200 step reel motor is activated

PrizeMotor.wav              - Played when the prize vend motor is started
Prizevend.wav               - Played when a prize is dispensed

PullHandleSlow.wav          - Played when button is configured as a Pull Handle type and Pulled
PullHandleNormal.wav        -
PullHandleFast.wav          -
PullHandleRelease.wav       - Played when button is configured as a Pull Handle and released

EM Effects
----------

MotorOn.wav                 - Played when the reel motor is activated
Wiper.wav                   - Played when a reel with studs is spinning
SolenoidOn.wav              - Played when a reel solenoid is activated
SolenoidOff.wav             - Played when a reel solenoid is deactivated
BellOn.wav                  - Played when the bell is switched on
BellOff.wav                 - Played when the bell is switched off
Buzzer.wav                  - Played when the nudge buzzer is activated
Sounder.wav                 - Played when the feature sounder is activated


The search order for effect files when loading a game is as follows:

1) The game folder
2) The DefaultSounds\Custom\"tech"\"cabinet style" folder (Where cabinet style is either blank, "Rio", "Genesis" or "Eclipse" etc)
3) The DefaultSounds folder

This provides a means to override any or all the default files either on a individual game, a tech or a tech and cabinet style basis