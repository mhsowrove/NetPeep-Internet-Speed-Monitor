(function(root,factory){const api=factory();if(typeof module==='object'&&module.exports)module.exports=api;else root.PeepFormat=api;})(typeof globalThis!=='undefined'?globalThis:this,function(){
  const units=['B','KB','MB','GB'];
  function format(value,unit='auto'){
    if(!Number.isFinite(value))return '—';value=Math.max(0,value);
    const index=unit==='auto'?Math.min(3,Math.floor(Math.log(Math.max(1,value))/Math.log(1000))):units.indexOf(unit);
    const i=index<0?0:index,n=value/1000**i;
    const digits=i===0?0:n===0?1:n<.001?9:n<1?4:n<100?2:1;
    return `${n.toFixed(digits).replace(/(\.\d*?[1-9])0+$|\.0+$/,'$1')} ${units[i]}`;
  }
  function preferences(patch){const result={};for(const[key,allowed]of Object.entries({unit:['auto',...units],theme:['system','light','dark']}))if(key in patch){if(!allowed.includes(patch[key]))throw Error('Invalid '+key);result[key]=patch[key];}return result;}
  return {format,preferences};
});
