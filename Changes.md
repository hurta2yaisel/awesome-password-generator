# Changes (what's new) #

### 06/14/2013 Version 1.4.0 ###
  * [Password strength meter](Strength.md) now has a tooltip with combinations number, password length in bits and maximum crack time for the currently selected password options;

![http://awesome-password-generator.googlecode.com/svn/wiki/pics/StrengthMeterTooltip.png](http://awesome-password-generator.googlecode.com/svn/wiki/pics/StrengthMeterTooltip.png)

  * Borders of password strength "classes" are changed (see [Password strength](Strength.md) section);
  * Bug fixed: Password strength meter now shows a correct reduced strength when **Exclude confused characters** or **Easy-to-type passwords** options are selected. Strength is also reduces while generating common (not an easy-to-type) passwords because of excluding weak passwords (which doesn't include all selected charsets). (See [Password strength](Strength.md) and [Options](Options.md) sections);
  * Bug fixed: Bias problem in common passwords (some passwords were generated more often then others);
  * Bug fixed: Bias problem in easy-to-type passwords (some passwords were generated more often then others);
  * Minor changes.
Sincere gratitude to Michael Samuel, who found those bugs and helped me fix them!

### 08/22/2012 Version 1.3.2 ###
  * Bugs fixed;
  * Minor changes.

### 07/01/2012 Version 1.3.1 ###
  * Bugs fixed.

### 02/19/2012 Version 1.3.0 ###
  * Hotkeys: now you can re-generate password by pressing **F5** or **Ctrl+R**, copy it to clipboard on **Ctrl+C** or **Ctrl+Ins**, and close window on **Esc**;
  * From now on you can't run multiple app instances simultaneously;
  * "Can't save configuration!" error message is now suppressed in the portable mode;
  * Minor changes.

### 01/20/2012 Version 1.2.0 ###
  * QuickGen feature: Now you can generate passwords via taskbar button's menu on Windows 7/8!

![http://awesome-password-generator.googlecode.com/svn/wiki/pics/QuickGenFeature.png](http://awesome-password-generator.googlecode.com/svn/wiki/pics/QuickGenFeature.png)

  * Minor changes.

### 12/16/2011 Version 1.1.0 ###
  * New feature: Now application is available in two versions: Windows Installer and Portable. You can use Portable version without administrative rights;
  * New feature: Windows taskbar progress indicator (in the bulk generation mode);

![http://awesome-password-generator.googlecode.com/svn/wiki/pics/TaskbarProgressBar.png](http://awesome-password-generator.googlecode.com/svn/wiki/pics/TaskbarProgressBar.png)

  * Bug fixed: User without administrative rights can't save passwords in the default "c:\passwords.txt" file. Now default locations of this file has changed;
  * Bug fixed: App crash on exit if user don't have administrator rights;
  * Minor changes.

### 12/04/2011 Version 1.0.0 ###
  * First release.