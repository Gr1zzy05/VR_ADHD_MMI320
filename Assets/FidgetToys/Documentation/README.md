# Butter Squishy และ Rainbow Pop It

เปิด `Assets/FidgetToys/Scenes/FidgetToyDemo.unity` แล้วกด Play ของเล่นทั้งสองวางอยู่บนโต๊ะเดียวกัน

## เล่นด้วยเมาส์

คลิก Game View ให้รับอินพุต แล้วชี้เมาส์ที่ของเล่น

| อินพุต | ผล |
|---|---|
| คลิกซ้ายค้างบนเนย | บีบเนย ปล่อยเมาส์เพื่อให้คืนรูป |
| คลิกซ้ายบนฟอง | กดกลับด้าน คลิกอีกครั้งเพื่อคืนด้านเดิม |
| ลากคลิกขวาบนของเล่น | ยกและย้ายตามเมาส์ ปล่อยเพื่อให้ตกตามฟิสิกส์ |
| F ระหว่างถือ | พลิก 180° เพื่อดูด้านหลัง |
| R | คืนฟองทั้ง 28 ฟองสู่สถานะนูน |

`ToyDemoMouse` อยู่บนกล้องเดสก์ท็อปของฉากนี้ ตัวสคริปต์ใช้ Input System ของโปรเจกต์และต้องการเคอร์เซอร์อิสระ

## เล่นด้วย VR

ใช้ Grip เพื่อหยิบวัตถุและปล่อย Grip เพื่อวาง สำหรับเนย ให้กด Trigger ของมือที่ถือค้างแล้วปล่อย สำหรับ Pop It ให้ถือบอร์ดด้วยมือหนึ่ง หันปลายคอนโทรลเลอร์อีกมือเข้าหาฟองในระยะประมาณ 13 ซม. แล้วกด Trigger หนึ่งครั้งต่อการกลับด้าน เล่นได้จากทั้งสองด้าน เสียง Pop และแรงสั่นปรับได้บนตัวบอร์ด

ฉากทดสอบใช้ Quest Player prefab และ PlayerModeRouter ของ HomeTasks สำหรับทดสอบผ่าน Quest Link ใช้เมนูเดิม **Home Focus → Quest Link play mode → Enable** แล้วเข้า Play เมื่อจะทดลองด้วยเมาส์ให้กลับสู่โหมด Desktop ของโปรเจกต์

## ใช้ในฉากหลัก

ลาก `Prefabs/ButterSquishy.prefab` และ `Prefabs/RainbowPopIt.prefab` เข้าฉาก วางเหนือผิวโต๊ะที่มี Collider เล็กน้อย ใช้ Scale (1, 1, 1) และ XR Interaction Manager กับ Near-Far Interactor เดิมบน Interaction Layer Default โหมด VR ทำงานจาก prefab ส่วนชุดเมาส์ของฉากสาธิตใช้งานบนกล้องที่มีเคอร์เซอร์อิสระ

## ไฟล์และการปรับ

- `Models/ButterSquishy/ButterSquishy.fbx` เป็นโมเดลขนาด 16 × 6 × 4 ซม. พร้อม Blend Shape ชื่อ Squeeze และ UV สำหรับฉลากพิมพ์
- `Models/RainbowPopIt/RainbowPopIt.fbx` มีกรอบซิลิโคนสองหน้า ช่องเปิด ขอบ และโดม 28 ชิ้นพร้อม Blend Shape ชื่อ Invert ส่วน `GrabHull.asset` เป็นรูปทรงชนแปดเหลี่ยมที่ใช้ร่วมกันตลอดการเล่น
- `Materials` เก็บวัสดุ URP ของของเล่นและโต๊ะ พร้อมภาพพิมพ์ ButterPrint ส่วน `Audio/SoftPop.wav` เป็นเสียงสังเคราะห์สั้นสำหรับแต่ละฟอง
- `ButterSquishyController.cs` ควบคุม Maximum Compression, Squeeze Speed, Recovery Speed และ Softness บน root ของเนย
- `PopItController.cs` รับตำแหน่งกดจากคอนโทรลเลอร์และกล้อง เลือกฟองในพิกัดของบอร์ด และควบคุมเสียงกับแรงสั่น ปรับ Press Animation Duration เป็นวินาที และ Bubble Depression Depth เป็นสัดส่วนระยะพลิกเต็มประมาณ 18 มม. ใช้เมนู Component **Reset All Bubbles** ระหว่าง Play หรือเรียก `ResetAllBubbles()` จากสคริปต์
- `PopItBubble.cs` เก็บสถานะและขยับ Blend Shape ของโดมแต่ละชิ้น แอนิเมชันหยุดอัปเดตเมื่อถึงเป้าหมาย
- `ToyDemoMouse.cs` จัดการอินพุตเมาส์และคีย์บอร์ดในฉากสาธิต
- `Editor/ToyAssetSetup.cs` มีเมนู **Home Focus → Fidget Toys → Create Missing Assets** สำหรับสร้าง prefab, วัสดุ และฉากสาธิตที่ขาด เมนูใช้ asset ที่มีอยู่เดิมเมื่อพบไฟล์ตรงตำแหน่ง
- ต้นฉบับแก้ไขได้อยู่ที่ `Tools/FidgetToys/ButterSquishy.blend` และ `Tools/FidgetToys/RainbowPopIt.blend` พร้อม `model.py` สำหรับสร้างเรขาคณิตและส่งออก FBX และ `make_print.py` สำหรับสร้างลายพิมพ์ สคริปต์สร้างโมเดลเขียนผลลัพธ์ที่เส้นทางเดิมเมื่อสั่งรันเอง

ของเล่นทั้งสองใช้ Rigidbody เพียงชิ้นละหนึ่งตัว การบีบและกดเปลี่ยนรูปเฉพาะ mesh ที่แสดงผล Collider คงรูปเพื่อให้การจับขณะเปลี่ยนรูปมีเสถียรภาพ

