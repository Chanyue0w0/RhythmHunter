// Run with FMOD Studio 2.02.31 fmodstudiocl -script <this file> <project.fspro>.
// Preserves the authored song, tempo and markers; creates one conditional native loop.
var event = studio.project.lookup("{6eee4bf1-1dfb-4dbe-a009-827539738fb6}");
if (!event) throw new Error("Ritual Slam event missing");
var markers = studio.project.model.NamedMarker.findInstances();
function marker(name) {
    var found = markers.filter(function(m) { return m.timeline.id === event.timeline.id && m.name === name; });
    if (found.length !== 1) throw new Error("Expected one " + name);
    return found[0].position;
}
var start = marker("Overtime_Start"), end = marker("Overtime_End");
if (!(end > start)) throw new Error("Invalid overtime region");
var preset = studio.project.lookup("parameter:/Overtime");
var parameter = preset ? event.parameters.filter(function(p) { return p.preset.id === preset.parameter.id; })[0] : null;
if (!parameter) parameter = event.addGameParameter({name:"Overtime",type:studio.project.parameterType.User,min:0,max:1});
var loop = studio.project.model.LoopRegion.findInstances().filter(function(r) { return r.timeline.id === event.timeline.id && r.name === "Overtime_Loop"; })[0];
if (!loop) {
    loop = studio.project.create("LoopRegion");
    loop.name = "Overtime_Loop";
    loop.timeline = event.timeline;
    loop.markerTrack = event.addMarkerTrack();
    loop.addParameterCondition(parameter.preset, .5, 1);
}
loop.position = start;
loop.length = end - start;
studio.project.save();
console.log("Overtime_Loop configured: " + start + " -> " + end);


