# RimWorld-LWM-Multi-Fuel-Requirement
LWM: Multi Fuel Requirement - a helper mod for [RimWorld](https://rimworldgame.com/) that allows other mod authors to create buildings that require two or more fuel sources to work.

For example, you could have a coal-powered paper nano-lathe that continually produces paper hats, and requires both coal AND wood to run.  Or maybe a steam power generator that runs on wood and fairy dust.  If you can make a building run on one fuel, now you can make it require two!

## Use
Put this mod above whatever mod requires the multiple fuels.

## Mod Makers
You can either point users towards this mod, or add the Assemblies folder to your own mod.  Feel free, have fun, use it as you like.  It would be nice if you acknowledged LWM somewhere in your mod.

## Mod XML
Instead of having a single comp of ```<li Class="CompProperties_Refuelable">...etc...</li>```, have multiple entires of 
```
<li Class="LWM.Multi_Fuel_Requirement.Properties">...etc1...</li>
<li Class="LWM.Multi_Fuel_Requirement.Properties">...etc2...</li>
```
Don't mix them.

## Known Limitations
 * Launchers:  (Pod Launchers, etc):  These may not work as expected.  If all the fuels burn at the same rate, it might work okay?  If this is a big thing for you, I might be able to make it happen?  But don't count on it.
 * Target Fuel Level: This goes with Launchers - it might work, especially if everything burns at the same rate and has the same maximum capacity.
 * Explosions:  Sometimes things go wrong, and your NanoForge(Hat Maker, whatever) takes some damage and explodes.  This IS a RimWorld, after all.  Due to limitations in patching, you'll probably get a smaller explosion than all those fuel tpes warrants - only the smallest fuel will blow up.  Sorry about that - another limitation of what I can patch.  If the code is ever adjusted to make patching easier, I could make bigger explosions.

Code available at: https://github.com/lilwhitemouse/RimWorld-LWM-Multi-Fuel-Requirement

Any problems, let me know in the forum, on GitHub, or if you get no response, at lilmouse(at)littlewhitemouse.net.
