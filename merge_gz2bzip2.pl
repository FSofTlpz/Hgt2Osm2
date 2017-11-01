# fasst die als Argumente gelieferten GZ-Dateien von HGT2OSM zu einer einzigen BZIP2-komprimierten
# Datei merge.osm.bz2 zusammen
# (nur sinnvoll, wenn in den Ausgangsdateien die ID's wirklich eindeutig sind)

use strict;

use IO::Uncompress::Gunzip qw(gunzip $GunzipError);
use IO::Compress::Bzip2 qw(bzip2 $Bzip2Error);

my $tmp = 'tmp.osm';
my $merge = 'merge.osm.bz2';

unlink $merge;
my $bz = new IO::Compress::Bzip2 $merge || die "IO::Compress::Bzip2 failed: $Bzip2Error\n";
open(TMP, ">$tmp") || die $!;

print STDERR "erzeuge $merge ...\n";

print $bz "<?xml version='1.0' encoding='UTF-8'?>\n";
print $bz "<osm version='0.6' generator='HGT2OSM'>\n";
for (my $i=0; $i<@ARGV; $i++) {
   HandleFile($ARGV[$i], $bz);
}
close(TMP);
print STDERR "schreibe WAY's ...\n";
open(TMP, $tmp) || die $!;
while (<TMP>) {
   print $bz $_;
}
close(TMP);
print $bz "</osm>\n";
$bz->close();
unlink $tmp;


sub HandleFile {
my ($inpfile, $merge) = @_;
my $gz = new IO::Uncompress::Gunzip $inpfile || die "IO::Uncompress::Gunzip failed: $GunzipError\n";
my $min = 0xffffffff;
my $max = 0;
   print STDERR "lese $inpfile ...\n";
   while (<$gz>) {
      my $line = $_;
      if ($line =~ /<node id='(\d+)'/) {
         print $merge $line;
         if ($max < $1) { $max = $1; }
         if ($min > $1) { $min = $1; }
      } else {
         my $test = substr($line, 0, 4);
         if ($test eq '<way' ||
             $test eq '<nd ' ||
             $test eq '<tag' ||
             $test eq '</wa') {
         print TMP $line;
      }
   }
   print STDERR "      Node-ID's: $min ... $max\n";
}
