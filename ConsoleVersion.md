# Console version (apg-cl.exe) #



## Purpose ##

Created for using in the scripts and has same functionality as GUI version of generator.

## Usage ##

![http://awesome-password-generator.googlecode.com/svn/wiki/pics/cmdlineBuilderUsage.png](http://awesome-password-generator.googlecode.com/svn/wiki/pics/cmdlineBuilderUsage.png)

  1. Run GUI version (`Awesome Password Generator.exe`), set up password length, quantity, charsets and other options as you wish;
  1. Click **Command line builder**, choise destination: console or file (file name from the **Bulk password generation** section will be used);
  1. Run `apg-cl.exe` with generated parameters. For example, command
```
apg-cl.exe -q:5 -l:10 "-c:enL,digits" -d:c
```
> will generate (in console) 5 passwords 10 symbols long with digits and lowercase letters.

## Exit codes (errorlevel) ##
| **Code** | **Description** |
|:---------|:----------------|
| 0        | OK              |
| 1        | Error in the cmdline parameters |
| 2        | Can't allocate memory |
| 3        | Can't save passwords in the file |

<font color='red'>Warning:</font> if `apg-cl.exe` execution fails, the error message will be printed to console. Keep it in mind when generating passwords to the console – it is possible to get an error message instead of passwords! Check the `errorlevel`!

## Usage examples in batch, PowerShell and C# ##

### Command line and batch-script ###

Let's generate 5 passwords into the file `c:\passwords.txt`:
```
@echo off

apg-cl.exe -q:5 -l:10 "-c:enL,digits" -ett -ecc "-d:f:r:c:\passwords.txt"

if %errorlevel%==0 goto okey
echo.
echo Some error!
exit

:okey
type c:\passwords.txt
```
Result:
```
1pyyi44wew
6afplyx69w
52p5316x8l
3530fe72e2
dmp4tj7h5o
```

If you need to process generated passwords "on the fly" without intermediate file, use script like this:
```
@echo off
setlocal enabledelayedexpansion

set c=0
for /f "usebackq delims=" %%i in (`"apg-cl.exe -q:5 -l:10 -c:enL,digits -d:c"`) do (
	set /a c=!c!+1
	echo Password #!c!: %%i
)
```
Result:
```
Password #1: 1pyyi44wew
Password #2: 6afplyx69w
Password #3: 52p5316x8l
Password #4: 3530fe72e2
Password #5: dmp4tj7h5o
```

<font color='red'>Note</font> the double-quotes nested inside the single-quotes (grave accents, ~ button). And double quotes **inside** the command have been **removed**!

### PowerShell ###

Let's generate 5 passwords into the file c:\passwords.txt:
```
./apg-cl.exe -q:5 -l:10 "-c:enL,digits" -ett -ecc "-d:f:r:c:\passwords.txt"

if($LASTEXITCODE -ne 0)
{
	write-host "`nSome error!" -foreground red
	exit
}

get-content c:\passwords.txt
```
Result:
```
1pyyi44wew
6afplyx69w
52p5316x8l
3530fe72e2
dmp4tj7h5o
```

The same but without the file - process all passwords in the script:
```
$passwords=./apg-cl.exe -q:5 -l:10 "-c:enL,digits" -ett -ecc -d:c

if($LASTEXITCODE -ne 0)
{
	write-host "Some error!" -foreground red
	$passwords	# display apg-cl output
	exit
}

$c=0
foreach($password in $passwords)
{
	$c++
	echo "Password #$c`: $password"
}
```
Result:
```
Password #1: 1pyyi44wew
Password #2: 6afplyx69w
Password #3: 52p5316x8l
Password #4: 3530fe72e2
Password #5: dmp4tj7h5o
```

### C# ###

under construction

## Limitations and warnings ##

Although GUI- and console versions functionalities are same, console version has some problems with user defined charset (`-udc` or `--userDefinedCharset` parameter) and with file names (`-o:f:` or `--destination:file:` parameter):
  * Impossibility of unicode symbols usage;
  * Problems with special symbols: they are not recommended to use.