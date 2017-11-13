# Hgt2Osm2
This tool use HGT-files and build OSM-contourlines.

I use this in a batch file:

      hgt2osm --HgtPath=path2newhgt --WriteElevationType=false --FakeDistance=-0.5 --MinVerticePoints=3 --MinBoundingbox=0.00016 --DouglasPeucker=0.12 --OutputOverwrite=true
      FOR %%f IN (*.osm.gz) DO call gz2pbf.cmd %%f

For all HGT's in the directory path2newhgt were build new files with names like clN40W006.osm.gz.
As next, all files where converted and compressed with gz2pbf.cmd to PBF's.

      @set str=%1
      @set str=%str:~0,-7%
      osmosis --rx %str%.osm.gz --wb %str%.osm.pbf omitmetadata=true
      del %1.osm.gz

Just we have for every HGT a new PBF with contoulines for 1°x1°.

For a new map we can take the downloaded map PBF and the necessary contour PBF's and merge the files with osmosis.

      osmosis --rbf germany.osm.pbf --rbf clN54E008.osm.pbf --merge ... --write-pbf file=map.osm.pbf omitmetadata=true

This new map.osm.pbf is the source für mkgmap.

Of course, you can prebuild a contour PBF for your country. And you clip this PBF to the country border. But this is another job.
