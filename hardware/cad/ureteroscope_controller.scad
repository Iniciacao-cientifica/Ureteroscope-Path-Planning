// Parametric desktop ureteroscope controller fixture.
// Units: millimetres. Print in PETG or PLA, 0.2 mm layers, 4 perimeters.
// Select one part by index with -D export_part=3 (see dispatch table at the end).

$fn = 36;
export_part = 0;
rod_diameter = 10;
print_tolerance = 0.30;
encoder_shaft_diameter = 6;
m3_clearance = 3.4;

module rounded_box(size, radius) {
  hull() {
    for (x = [radius, size[0] - radius])
      for (y = [radius, size[1] - radius])
        translate([x, y, 0]) cylinder(h = size[2], r = radius);
  }
}

module handle_body() {
  difference() {
    rounded_box([105, 42, 27], 6);
    translate([3, 3, 3]) rounded_box([99, 36, 25], 4);
    // USB cable, two 12 mm pushbuttons and four M3 lid screws.
    translate([-1, 21, 13.5]) rotate([0, 90, 0]) cylinder(h = 7, d = 10);
    for (x = [34, 70]) translate([x, -1, 14]) rotate([-90, 0, 0]) cylinder(h = 6, d = 12.4);
    for (x = [8, 97]) for (y = [8, 34]) translate([x, y, -1]) cylinder(h = 8, d = 2.7);
  }
  // ESP32-S3 board supports: nominal board 69 x 28 mm.
  for (x = [20, 85]) for (y = [9, 33]) translate([x, y, 3]) cylinder(h = 4, d = 5);
  // IMU shelf kept near the probe axis.
  translate([9, 13, 3]) cube([18, 16, 2]);
}

module handle_lid() {
  difference() {
    rounded_box([105, 42, 3], 6);
    for (x = [8, 97]) for (y = [8, 34]) translate([x, y, -1]) cylinder(h = 5, d = m3_clearance);
  }
}

module guide_base() {
  difference() {
    union() {
      rounded_box([92, 68, 8], 5);
      translate([11, 34, 8]) rotate([0, 90, 0]) cylinder(h = 70, d = 25);
      // Encoder pivot cheeks.
      translate([28, 6, 8]) cube([8, 16, 25]);
      translate([56, 6, 8]) cube([8, 16, 25]);
    }
    // Replaceable guide bushing bore.
    translate([10, 34, 8]) rotate([0, 90, 0]) cylinder(h = 72, d = 18.3);
    // M3 pivot and spring preload screw.
    translate([26, 14, 25]) rotate([0, 90, 0]) cylinder(h = 40, d = m3_clearance);
    translate([46, -1, 15]) rotate([-90, 0, 0]) cylinder(h = 18, d = m3_clearance);
    // Four table fixing holes.
    for (x = [8, 84]) for (y = [8, 60]) translate([x, y, -1]) cylinder(h = 11, d = 4.5);
  }
}

module guide_bushing() {
  difference() {
    union() {
      cylinder(h = 24, d = 18);
      cylinder(h = 3, d = 24);
    }
    translate([0, 0, -1]) cylinder(h = 27, d = rod_diameter + 2 * print_tolerance);
  }
}

module encoder_arm() {
  difference() {
    hull() {
      translate([0, 0, 0]) cylinder(h = 8, d = 20);
      translate([45, 0, 0]) cylinder(h = 8, d = 24);
    }
    translate([0, 0, -1]) cylinder(h = 10, d = m3_clearance);
    translate([45, 0, -1]) cylinder(h = 10, d = encoder_shaft_diameter + print_tolerance);
    // Two slots accept the KY-040/EC11 board or M3 zip-tie anchors.
    for (x = [29, 38]) translate([x, -8, -1]) cube([3.5, 16, 10]);
  }
}

module encoder_wheel() {
  difference() {
    union() {
      cylinder(h = 8, d = 20);
      translate([0, 0, 2]) cylinder(h = 4, d = 23);
    }
    translate([0, 0, -1]) cylinder(h = 10, d = encoder_shaft_diameter - 0.15);
    // Groove for a 20 x 2 mm O-ring or rubber band.
    rotate_extrude() translate([10.5, 4, 0]) circle(d = 2.2, $fn = 24);
  }
}

module assembly() {
  color("SlateGray") guide_base();
  color("LightSkyBlue") translate([10, 34, 20.5]) rotate([0, 90, 0]) guide_bushing();
  color("Orange") translate([46, 14, 26]) rotate([90, 0, 0]) encoder_arm();
  color("Black") translate([46, 34 + rod_diameter / 2 + 10, 21]) rotate([90, 0, 0]) encoder_wheel();
  color("Silver") translate([-25, 34, 20.5]) rotate([0, 90, 0]) cylinder(h = 145, d = rod_diameter);
}

if (export_part == 1) handle_body();
else if (export_part == 2) handle_lid();
else if (export_part == 3) guide_base();
else if (export_part == 4) guide_bushing();
else if (export_part == 5) encoder_arm();
else if (export_part == 6) encoder_wheel();
else assembly();
