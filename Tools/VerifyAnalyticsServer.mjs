// Loopback-only fixture launched by VerifyAnalyticsOutbox.ps1. Uses the production API and migrations.
import assert from 'node:assert/strict'
import { randomBytes } from 'node:crypto'
import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import https from 'node:https'
import net from 'node:net'
import { join } from 'node:path'
import { setTimeout as delay } from 'node:timers/promises'
import { application } from '../Analytics/server/http.ts'
import { Store } from '../Analytics/server/store.ts'

const project = process.argv[2]
const database = join(project, 'synthetic.sqlite')
async function waitFor(name) {
  const deadline = Date.now() + 180000
  while (!existsSync(join(project, name))) {
    if (Date.now() > deadline) throw new Error('Timed out waiting for ' + name)
    await delay(50)
  }
}
function listen(server, port) {
  return new Promise((resolve, reject) => {
    server.once('error', reject)
    server.listen(port, '127.0.0.1', () => { server.off('error', reject); resolve() })
  })
}
function close(server) { return new Promise(resolve => server.close(resolve)) }

// Release the chosen port until the game has observed its two native connection failures.
const reserve = net.createServer()
await listen(reserve, 0)
const port = reserve.address().port
await close(reserve)
const origin = 'https://127.0.0.1:' + port
const store = new Store(database)
const staffPassword = () => randomBytes(32).toString('hex')
const enrollmentKey = randomBytes(32).toString('base64url')
const api = application(store, {
  origin, retentionDays: 7, grantDays: 1, demo: false,
  adminUser: 'synthetic-admin', adminPassword: staffPassword(), enrollmentKey,
  educatorUser: 'synthetic-educator', educatorPassword: staffPassword()
})
const server = https.createServer({ pfx: readFileSync(join(project, 'test-server.pfx')), passphrase: 'test-only' }, api.app)
try {
  await listen(server,port)
  writeFileSync(join(project, 'server-origin.txt'),origin)
  writeFileSync(join(project, 'enrollment.fixture'),enrollmentKey)
  writeFileSync(join(project, 'api.prepared'), '')
  await waitFor('enrollment-received')
  await close(server)
  writeFileSync(join(project,'api.offline'),'')
  await waitFor('enable-api')
  await listen(server, port)
  writeFileSync(join(project, 'api.online'), '')
  await waitFor('verified-receipts.json')
  const receipts = JSON.parse(readFileSync(join(project, 'verified-receipts.json'), 'utf8'))
  assert.deepEqual({first:receipts.first,replay:receipts.replay,next:receipts.next}, { first: 201, replay: 200, next: 201 })
  const reader = new Store(database)
  try {
    const { sessions, participants } = reader.records()
    assert.deepEqual(participants.map(p => p.code), ['P001','P002'])
    assert.equal(sessions.length, 2)
    const s = sessions.find(session => session.id === receipts.id)
    const subsequent = sessions.find(session => session.id === receipts.nextId)
    assert.equal(subsequent.participantId, participants[1].id)
    assert.equal(subsequent.durationSeconds, 42)
    assert.equal(s.participantId, participants[0].id)
    assert.equal(s.durationSeconds, 75.5)
    assert.equal(s.status, 'completed')
    assert.equal(s.missions.length, 4)
    assert(s.missions.every(m => m.completed && m.timeSeconds === null))
    assert.equal(s.distractions.length, 2)
    assert.equal(s.distractions.find(d => d.kind === 'Phone').count, 2)
    assert.equal(s.distractions.find(d => d.kind === 'Phone').seconds, 3.125)
    assert.equal(s.distractions.find(d => d.kind === 'Television').seconds, 0)
    assert.equal(s.quiz.correct, 2)
    assert.equal(s.quiz.incorrect, 1)
  } finally { reader.db.close() }
  console.log('FOCUS_SHIFT_SQLITE_PASS')
} finally {
  await close(server)
  api.close()
  store.db.close()
}
