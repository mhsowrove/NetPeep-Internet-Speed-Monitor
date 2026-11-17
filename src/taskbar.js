'use strict';
// Coordinates are Electron device-independent pixels.
function taskbarBounds(bar, notification, fraction) {
  const vertical=bar.height>bar.width;
  const width=vertical?Math.min(112,bar.width):112,height=vertical?38:Math.min(38,bar.height);
  const span=vertical?bar.height-height:bar.width-width;
  const suggested=vertical?(notification?notification.y-bar.y-height-10:span-180):(notification?notification.x-bar.x-width-12:span-180);
  const offset=Number.isFinite(fraction)?Math.round(Math.max(0,Math.min(1,fraction))*span):Math.round(Math.max(0,Math.min(span,suggested)));
  return {x:Math.round(bar.x+(vertical?(bar.width-width)/2:offset)),y:Math.round(bar.y+(vertical?offset:(bar.height-height)/2)),width:Math.round(width),height:Math.round(height)};
}
function positionFraction(bar,bounds) {const vertical=bar.height>bar.width,span=vertical?bar.height-bounds.height:bar.width-bounds.width;return span>0?Math.max(0,Math.min(1,(vertical?bounds.y-bar.y:bounds.x-bar.x)/span)):0;}
module.exports={taskbarBounds,positionFraction};
