************************************************************************

MFT - A collection of MFTs and a player to invoke them.

**************************************************************************

The purpose of this solution is to provide a generic framework to make 
writing MFTs (Media Foundation Transforms) easier.  If you don't know what 
an MFT is, visit 
https://msdn.microsoft.com/en-us/library/windows/desktop/ms703138%28v=vs.85%29.aspx 
for more info.

In particular, this framework helps create both synchronous and asynchronous 
MFTs that have exactly 1 input and 1 output stream.  Asynchronous MFTs have 
a variety of advantages over the older approach, including the ability to 
support multiple processing threads.

As you read the docs for AMFTs, don't be too discouraged if they appear to 
be complex.  Nearly all the work for the 'complex' stuff (sending/receiving 
events, dynamic format changes, markers, threading, unlocking, etc) is 
handled for you by the framework.

Essentially all you are responsible for is validating/enumerating what input 
and output types you support, and doing the actual 'transform.'

IMPORTANT: When building this solution, you need to be running VS as an 
administrator in order to perform the COM registrations.

When reading the code in the samples, you might start with FrameCounter.  
It is about the simplest MFT you can write using the framework.  However 
it doesn't look like much when run under PlaybackFX.  For something that
is more visually interesting, I recommend either Rotate or WriteText.
Rotate can change the rotation while the video is playing by pressing 'r'.

To understand what each of the transforms do, see the readme for each 
individual project.

For instructions on creating your own MFT, read HowTo.txt.

Also, before trying to build this project, you will need to register
lib\MediaFoundation.dll, with a command line like:

%windir%\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe /tlb MediaFoundation.dll
