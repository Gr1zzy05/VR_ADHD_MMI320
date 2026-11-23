# เปิด FOCUS SHIFT จากหลายเครื่องด้วย Vercel และ PostgreSQL

Vercel เปิดหน้าเว็บและรัน API ส่วน Neon ให้บริการฐานข้อมูล PostgreSQL กลาง เมื่อเปลี่ยนคอมพิวเตอร์ ให้เปิดเว็บ URL เดิมแล้วเข้าสู่ระบบอีกครั้ง ประวัติผู้เข้าร่วมและผล VR จะอ่านจากฐานข้อมูลเดียวกัน

## ตั้งค่าใน Vercel

เมื่อใช้ repository ส่วนตัวที่มีโค้ดเว็บอยู่ที่ราก เช่น `focus-shift-analytics` ให้เลือก **Root Directory → ./ (ราก repo)** ส่วน repository Unity ของทีมที่มีเว็บเป็นโฟลเดอร์ย่อยให้เลือก `Analytics`

ถ้า Import ค้นหา repo ไม่เจอ ให้ดูชื่อเจ้าของใน GitHub ก่อน โปรเจกต์นี้อยู่ใต้บัญชี `Gr1zzy05` สำหรับ repository ที่เป็นของบัญชีส่วนบุคคล Vercel ให้เจ้าของ repo เป็นผู้ Import โดยตรง เลือกได้ว่าจะให้เพื่อน Import หรือ deploy เฉพาะเว็บจากเครื่องด้วย Vercel CLI [GitHub ของโปรเจกต์](https://github.com/Gr1zzy05/VR_ADHD_MMI320) · [สิทธิ์ในการ Import ของ Vercel](https://vercel.com/docs/git/vercel-for-github#missing-git-repository)

1. **GitHub** เปิด repository ของทีมแล้วดูว่า branch ที่จะ deploy มีไฟล์ `Analytics/vercel.json` ถ้ายังเห็นไฟล์นี้เฉพาะในเครื่อง ให้ commit และ push งาน Analytics ไปที่ branch ของทีมก่อน หากใช้ Pull Request ให้ merge เข้า branch Production หลังทีมตรวจ
2. **Vercel** เปิดหน้า New Project และเข้าสู่ระบบด้วย GitHub กด **Import** ที่ repository ของทีม ในหน้า **Configure Project** กด **Edit** ข้าง **Root Directory** เลือก `Analytics` แล้วเลือก **Vite** และ **Node.js 24.x** หากระบบแสดงตัวเลือก [เปิดหน้า Vercel New Project](https://vercel.com/new) · [วิธี Import Git repository](https://vercel.com/docs/git)
3. ในหน้าตั้งค่าก่อนกด **Deploy** เพิ่ม `ADMIN_PASSWORD`, `EDUCATOR_PASSWORD` และ `CRON_SECRET` ตามตารางด้านล่าง จากนั้นกด **Deploy** เพื่อสร้างโปรเจกต์ หน้า Login จะพร้อมใช้งานหลังเชื่อมฐานข้อมูล
4. เปิดโปรเจกต์ที่สร้างแล้ว ไปที่ **Storage → Create Database → Neon** สร้างฐานข้อมูลและเชื่อมกับโปรเจกต์ จากนั้นเปิด **Settings → Environment Variables** เช็กว่า Production มี `DATABASE_URL` ของ Neon หากจะใช้ Preview เพื่อทดสอบ ให้ใช้ฐานข้อมูลแยกจาก Production [Neon บน Vercel](https://vercel.com/marketplace/neon)
5. ไปที่ **Deployments** กดเมนู **…** ของ Production Deployment แล้วเลือก **Redeploy** เพื่อโหลดค่าใหม่ พอขึ้นสถานะ **Ready** ให้เปิดโดเมนหลักจากหน้า **Domains** แล้วลองล็อกอินผู้ดูแล ค่าเริ่มต้นของชื่อผู้ใช้คือ `admin` [วิธี Redeploy](https://vercel.com/docs/deployments/managing-deployments#redeploy-a-project)

## ค่าที่ใช้ใน Environment Variables

| ตัวแปร | ค่าที่ต้องใส่ |
| --- | --- |
| `DATABASE_URL` | PostgreSQL connection string ที่ได้จาก Neon |
| `PUBLIC_ORIGIN` | ตั้งได้เมื่อเลือกโดเมนเฉพาะ เช่น `https://focus-shift-your-team.vercel.app` หากเว้นไว้ ระบบใช้โดเมนที่ Vercel ส่งให้ผ่าน System Environment Variables |
| `ADMIN_USER` | ชื่อเข้าระบบของผู้ดูแล ค่าเริ่มต้น `admin` |
| `ADMIN_PASSWORD` | รหัสผ่านที่ตั้งเอง ยาวอย่างน้อย 16 ตัวอักษร |
| `EDUCATOR_USER` | ชื่อเข้าระบบของผู้สอน ค่าเริ่มต้น `educator` |
| `EDUCATOR_PASSWORD` | รหัสผ่านคนละชุดกับผู้ดูแล ยาวอย่างน้อย 16 ตัวอักษร |
| `CRON_SECRET` | ค่าสุ่มสำหรับ Vercel Cron ใช้ตัวอักษรอังกฤษ ตัวเลข ขีดกลางหรือขีดล่าง อย่างน้อย 16 ตัวอักษร |
| `GAME_ENROLLMENT_KEY` | คีย์สำหรับอนุญาตให้ Build ขอรหัสผู้เล่น สร้างด้วย randomBytes(32) แบบ base64url (43 ตัว) ใส่ค่าเดียวกันในเมนูตั้งค่า Build ของ Unity ใช้แยกจากรหัสผ่านเจ้าหน้าที่ |
| `RETENTION_DAYS`, `GRANT_DAYS` | ตั้งเพิ่มได้ ค่าเริ่มต้น 180 วันสำหรับผลการเล่นและ 7 วันสำหรับสิทธิ์ส่งผลจากเกม |

สร้างค่าสุ่มสำหรับแต่ละรหัสด้วยคำสั่งนี้ รันแยกกันสำหรับแต่ละค่า

```powershell
node -e "console.log(require('node:crypto').randomBytes(24).toString('base64url'))"
```

API จะสร้างตาราง PostgreSQL ในการเรียกใช้ครั้งแรก หากเลือกใช้โดเมนที่ Vercel ตั้งให้ ให้เปิด **Automatically expose System Environment Variables** ในการตั้งค่าโปรเจกต์ [เอกสาร System Environment Variables](https://vercel.com/docs/environment-variables/system-environment-variables)

ตรวจว่าโดเมน Production เปิดให้ Quest เรียก `/api/submissions` ผ่านอินเทอร์เน็ตได้ หากเปิด Deployment Protection ระดับ Vercel ไว้กับโดเมนนี้ คำขอจาก Quest อาจติดหน้าล็อกอินของ Vercel ก่อนเข้าถึง API สิทธิ์ของ FOCUS SHIFT ตรวจสอบจากบัญชีเจ้าหน้าที่และสิทธิ์สำหรับแต่ละรอบ [เอกสาร Deployment Protection](https://vercel.com/docs/deployment-protection)

ฐานข้อมูลเดโม SQLite ในเครื่องใช้สำหรับทดลอง หากมีผลการเล่นจริงอยู่ใน SQLite จากการติดตั้งอื่น ให้ย้ายข้อมูลก่อนเปลี่ยนไปใช้ฐานข้อมูลกลาง การตั้ง `DATABASE_URL` จะเริ่มอ่านจาก PostgreSQL ของ URL นั้น

## Deploy จากเครื่องผ่าน Vercel CLI

ทางนี้ใช้บัญชี Vercel ของคุณและอัปโหลดเฉพาะโค้ดเว็บจากเครื่อง เปิด PowerShell ที่โฟลเดอร์หลักของโปรเจกต์แล้วรัน

```powershell
$source = Join-Path (Get-Location).Path 'Analytics'
$deploy = Join-Path $env:TEMP ('FocusShift-Vercel-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $deploy | Out-Null
$items = 'api','client','server','migrations','index.html','package.json','package-lock.json','tsconfig.json','vite.config.ts','vercel.json'
foreach ($item in $items) {
    Copy-Item -LiteralPath (Join-Path $source $item) -Destination $deploy -Recurse -ErrorAction Stop
}
Set-Location $deploy
npx.cmd vercel@latest login
npx.cmd vercel@latest --prod
```

เมื่อ CLI ถามให้ตั้งค่า ให้เลือกบัญชีของคุณ สร้าง Project ใหม่ และใช้โฟลเดอร์ปัจจุบัน `./` จากนั้นเข้า Dashboard ของโปรเจกต์เพื่อตั้งรหัสผ่านและเชื่อม Neon ตามตารางข้างบน แล้ว Redeploy เพื่อใช้ค่าที่เพิ่งเพิ่ม เก็บโฟลเดอร์นี้ไว้ถ้าจะ deploy โปรเจกต์เดิมอีกครั้ง เพราะ CLI จดจำ Project ที่เชื่อมไว้ในโฟลเดอร์ `.vercel` [คู่มือ Vercel CLI](https://vercel.com/docs/cli/deploy)

## ใช้ชื่อและรหัสอะไรที่หน้า Login

- ผู้ดูแลใช้ค่าของ `ADMIN_USER` และ `ADMIN_PASSWORD` เพื่อจัดการผู้เข้าร่วม เตรียมรอบ VR และส่งออกรายงาน
- ผู้สอนใช้ค่าของ `EDUCATOR_USER` และ `EDUCATOR_PASSWORD` เพื่อดูประวัติและผลกิจกรรม
- ค่าที่เห็นในหน้าเดโมใช้กับเดโมในเครื่อง ผู้เข้าร่วมใช้รหัสอย่าง `P001` เพื่ออ้างถึงประวัติในการกลับมาเล่น

## เชื่อมเกมที่ Build แล้ว

หลังได้ URL จริง ทีมเปิดโปรเจกต์ Unity ระหว่างเตรียม Build ไปที่ **Home Focus → Set study server for builds** ใส่ URL แบบ HTTPS และค่า `GAME_ENROLLMENT_KEY` แล้วสร้าง APK สำหรับ Quest หรือ Build สำหรับ Windows ใหม่ ผู้เล่นเลือกเริ่มแบบเก็บผลหรือแบบทดลองจากในเกมได้เลย [ขั้นตอนใช้งาน Build](quickstart-th.md#3-เตรียม-build-ให้ส่งผลอัตโนมัติ)

Vercel Cron เรียกงานล้างข้อมูลตามกำหนดวันละครั้งผ่าน `/api/retention` งานนี้ใช้ `CRON_SECRET` ในการยืนยันคำขอ [เอกสาร Vercel Cron](https://vercel.com/docs/schedules/cron-jobs/manage-cron-jobs)
