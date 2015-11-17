# Options #
## Exclude confusing characters ##
Some symbols like 0 (zero) and O (letter) are very similar (difficult to distinguish) which cause difficulties when typing. This option makes it possible to exclude such symbols.

![http://awesome-password-generator.googlecode.com/svn/wiki/pics/excludeConfusingCharacters.png](http://awesome-password-generator.googlecode.com/svn/wiki/pics/excludeConfusingCharacters.png)

<font color='red'>Warning:</font> Excluding this symbols leads to reduced password strength (see the strength meter).

## Easy to type password ##
Strong passwords are usually long and contains symbols from different charsets: lowercase and uppercase letters, digits and special symbols. Such passwords are hard to type and even more hard to remember.

Solution is to generate password from lowercase letters (which are easiest to type – you don't even need to press Shift) with 1-2 symbols from each of another charsets:

![http://awesome-password-generator.googlecode.com/svn/wiki/pics/easytotype.png](http://awesome-password-generator.googlecode.com/svn/wiki/pics/easytotype.png)

<font color='red'>Warning:</font> Rejecting all not-an-easy-to-type passwords reduces total combinations quantity; you can see it in the strength meter.

## Common password (not an easy-to-type) ##
(That is **Easy to type password** option is not checked)

In this case you will get a common password without preference to any specific charset.
Common passwords are more secure than easy-to-type passwords of the same length and charsets.

It is one special feature however. Imagine you was selected 0..9 and a..z charsets for you password. Most password generators will generate either "strong" passwords with both numbers and letters and "weak" passwords with only digits or letters included. And if algorithm produces a weak password and you use it, attacker can decide to run a quick brute force with lesser charsets included and accidentally win the game.

Assuming you have checked both charsets for a reason, **Awesome Password Generator** will not generate "weak" passwords. It reduces total combinations number of course (strength meter reflects this reduction), but on the other hand you will only get strong passwords.