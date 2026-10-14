(() => {
 const data=window.CREATURE_DATA,byId=new Map(data.map(item=>[item.id,item]));
 const $=id=>document.getElementById(id),grid=$('grid'),dialog=$('detail');
 const groups={land:'陆地动物',small_aquatic:'小型与水生',enemies:'普通怪物',bosses:'精英与首领'};
 const clips={idle:'待机',walk:'行走',run:'奔跑',graze:'低头吃草',look:'观望',peck:'啄食',curl:'蜷缩蓄势',swim:'游动',dart:'快速摆动',pulse:'触腕脉动',charge:'冲锋蓄力',bite:'咬击',melee:'近战出手',bolt:'瞄准发射',slam:'砸击',explode:'爆炸蓄力'};
 let group='all',visible=data.map(item=>item.id),selected=null,playing=false,timer=null,frame=0,frames=[],generation=0;
 function stop(){playing=false;clearInterval(timer);timer=null;if(selected){$('motion').textContent='播放'+clips[selected.previewClip];$('motion').setAttribute('aria-pressed','false');}}
 function showFrame(n){frame=n;$('model').src=frames[n].src;$('timeline').value=n;$('frame').textContent=`${n+1} / 24`;}
 function filter(){
  const search=$('search').value.trim().toLowerCase();visible=[];
  data.forEach(item=>{const show=(group==='all'||group==='new'&&!item.acceptedPrototype||item.group===group)&&(!search||(item.name+' '+item.id).toLowerCase().includes(search));
   grid.querySelector(`[data-id="${item.id}"]`).hidden=!show;if(show)visible.push(item.id);});
  $('count').textContent=`显示 ${visible.length} / 26 种生物`;$('empty').hidden=visible.length>0;
  document.querySelectorAll('[data-group]').forEach(button=>button.setAttribute('aria-pressed',String(button.dataset.group===group)));
 }
 for(const item of data){
  const card=document.createElement('button');card.className='card';card.dataset.id=item.id;card.setAttribute('aria-label',`查看${item.name}，${item.triangles}三角面`);
  const img=document.createElement('img');img.src=`${item.id}/${item.id}-three-quarter.png`;img.alt=item.name+'实际模型';img.loading='lazy';
  const info=document.createElement('div');info.className='info';const title=document.createElement('h2');title.append(document.createTextNode(item.name));
  const metric=document.createElement('span');metric.textContent=item.triangles.toLocaleString('zh-CN')+' 三角面';title.append(metric);
  const description=document.createElement('p');description.textContent=`${groups[item.group]} · ${item.clips.length} 个动作${item.acceptedPrototype?' · 已确认小样':''}`;
  info.append(title,description);card.append(img,info);card.onclick=()=>open(item.id);grid.append(card);
 }
 function open(id){
  stop();selected=byId.get(id);generation++;const token=generation;frame=0;frames=[];
  $('name').textContent=selected.name;$('category').textContent=groups[selected.group]+' / '+id;
  $('metrics').textContent=`${selected.triangles.toLocaleString('zh-CN')} 三角面 · ${Math.round(selected.byteSize/1024)} KB GLB · ${selected.textureSize.join(' × ')} 贴图`;
  $('description').textContent=selected.description;$('clips').textContent='模型内含动作：'+selected.clips.map(name=>clips[name]||name).join('、')+'。页面演示：'+clips[selected.previewClip]+'。';
  $('model').src=`${id}/${id}-three-quarter.png`;$('model').alt=selected.name+'实际模型渲染';
  $('reference').src=`${id}/${id}-reference.png`;$('referencecaption').textContent=selected.referenceKind;
  $('referencewrap').hidden=true;$('pictures').classList.remove('comparing');$('compare').textContent='查看造型参考';
  $('framecontrol').hidden=true;$('timeline').value=0;$('frame').textContent='1 / 24';
  for(const [element,suffix] of [['glb','.glb'],['blend','.blend'],['atlas','-atlas.png'],['emission','-emission.png']])$(element).href=`${id}/${id}${suffix}`;
  $('emission').hidden=!selected.emissionMask;
  document.querySelectorAll('[data-view]').forEach(button=>button.setAttribute('aria-pressed',String(button.dataset.view==='three-quarter')));
  $('motion').disabled=true;$('motion').textContent='载入动作…';
  frames=Array.from({length:24},(_,n)=>{const img=new Image();img.src=`${id}/motion/${String(n).padStart(2,'0')}.png`;return img;});
  Promise.all(frames.map(img=>img.decode())).then(()=>{if(generation!==token)return;$('motion').disabled=false;stop();}).catch(()=>{if(generation===token)$('motion').textContent='动作载入失败';});
  if(!dialog.open)dialog.showModal();
 }
 document.querySelectorAll('[data-group]').forEach(button=>button.onclick=()=>{group=button.dataset.group;filter();});
 $('search').oninput=filter;
 document.querySelectorAll('[data-view]').forEach(button=>button.onclick=()=>{
  stop();$('framecontrol').hidden=true;$('model').src=`${selected.id}/${selected.id}-${button.dataset.view}.png`;
  document.querySelectorAll('[data-view]').forEach(other=>other.setAttribute('aria-pressed',String(other===button)));
 });
 $('motion').onclick=()=>{
  if(playing){stop();return;}$('framecontrol').hidden=false;playing=true;$('motion').textContent='暂停动作';$('motion').setAttribute('aria-pressed','true');showFrame(frame);
  document.querySelectorAll('[data-view]').forEach(button=>button.setAttribute('aria-pressed','false'));
  const duration=selected.previewClip==='walk'||selected.previewClip==='swim'?1000:1333;
  timer=setInterval(()=>showFrame((frame+1)%24),duration/24);
 };
 $('timeline').oninput=()=>{stop();showFrame(Number($('timeline').value));};
 $('compare').onclick=()=>{const showing=!$('referencewrap').hidden;$('referencewrap').hidden=showing;$('pictures').classList.toggle('comparing',!showing);$('compare').textContent=showing?'查看造型参考':'收起造型参考';};
 function adjacent(direction){const index=visible.indexOf(selected.id);open(visible[(index+direction+visible.length)%visible.length]);}
 $('previous').onclick=()=>adjacent(-1);$('next').onclick=()=>adjacent(1);
 $('close').onclick=()=>dialog.close();dialog.addEventListener('close',()=>{stop();generation++;});
 dialog.addEventListener('click',event=>{if(event.target===dialog){const rect=dialog.getBoundingClientRect();if(event.clientX<rect.left||event.clientX>rect.right||event.clientY<rect.top||event.clientY>rect.bottom)dialog.close();}});
 document.addEventListener('visibilitychange',()=>{if(document.hidden)stop();});filter();
})();
