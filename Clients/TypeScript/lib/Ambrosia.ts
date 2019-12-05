import { parseLogRecords, byteArrayToBuffer, byteArrayFromBuffer, serializeMessage, serializeLogRecord, parseLogRecord, wholeSlice } from './AmbrosiaFormat'
import { ByteArray, Message, uint64 } from './AmbrosiaFormat'
import { MsgCountReplayableRPCBatch, MsgUpgradeService, MsgTakeBecomingPrimaryCheckpoint, MsgUpgradeTakeCheckpoint } from './AmbrosiaFormat'
import { MsgInitialMessage, MsgCheckpoint, MsgRPCBatch, MsgTakeCheckpoint, MsgAttachTo, MsgRPC } from './AmbrosiaFormat'

import net = require("net");

type AmbrosiaLogEntry = Buffer
type AmbrosiaCheckpoint = Buffer

class Ambrosia {
  public buf: ByteArray = new ByteArray([])
  public replaying: boolean = true
  public sendSocket: any

  constructor(
    public onLogEntry: (logEntry: AmbrosiaLogEntry) => void,
    public onBecomePrimary: () => void,
    public onCheckpoint: () => AmbrosiaCheckpoint,
    public onRestoreCheckpoint: (checkpoint: AmbrosiaCheckpoint) => void,
    public host: string = "127.0.0.1",
    public sendPort: number = 1000,
    public receivePort: number = 1001
  ) {
    // Open client for sending messages to the Immortal Coordinator.
    this.sendSocket = new net.Socket();

    this.sendSocket.connect(sendPort, host, () => {
      this.trace("Connected to: " + host + ":" + sendPort)
    })

    // Wait for incoming connection from the Immortal Coordinator.
    net.createServer((sock: any) => {
      this.trace("Connected to: " + sock.remoteAddress + ":" + sock.remotePort)

      // Handle received data.
      sock.on('data', (data: any) => {
        this.trace("Received data from IC: " + data.toString('hex'))

        // Transfer bytes into shared buffer until we have parseable message.
        var tmpBuffer : number[] = []

        for(const d of data.values()) {
          tmpBuffer.push(d)
        }

        this.buf.concat(new ByteArray(tmpBuffer))

        let accumulated : Message[] = []
        try {
          let logRecords = parseLogRecords(wholeSlice(this.buf))
          for(var logRecord of logRecords) {
            for(var msg of logRecord.msgs) {
              accumulated.push(msg)
            }
          }

          this.buf = new ByteArray([])
        } catch (err) {
          this.trace("Parsing error: " + err)
        }
        for(var msg of accumulated)
        {
          this.handle(msg)
        }
      });
    }).listen(receivePort, host)
  }

  handle(message: Message) {
    this.trace("Handle invoked for message type: " + message.typ)

    if(message instanceof MsgCountReplayableRPCBatch) {
      throw new Error("Not implemented!")
    } 
    else if(message instanceof MsgUpgradeService) {
      throw new Error("Not implemented!")
    }
    else if(message instanceof MsgTakeBecomingPrimaryCheckpoint) {
      // Disable replaying.
      this.replaying = false

      // Create initial RPC message.
      let rpcMessage = new MsgRPC()
      rpcMessage.methodId = 0
      rpcMessage.isOutgoing = false

      // Create initial message containing the initial RPC message.
      let initialMessage = new MsgInitialMessage()
      initialMessage.rpc = rpcMessage

      // Send initial message.
      this.sendBuffer(initialMessage)

      // Send checkpoint.
      let checkpointMessage = new MsgCheckpoint()
      checkpointMessage.checkpoint = byteArrayFromBuffer(this.onCheckpoint())
      this.sendBuffer(checkpointMessage)
    }
    else if(message instanceof MsgUpgradeTakeCheckpoint) {
      throw new Error("Not implemented!")
    }
    else if(message instanceof MsgInitialMessage) {
      // Nothing.
    } 
    else if(message instanceof MsgCheckpoint) {
      let buffer = byteArrayToBuffer(message.checkpoint)
      this.onRestoreCheckpoint(buffer)
    }
    else if(message instanceof MsgRPCBatch) {
      throw new Error("Not implemented!")
    } 
    else if(message instanceof MsgTakeCheckpoint) {
      // Send checkpoint.
      let checkpointMessage = new MsgCheckpoint()
      checkpointMessage.checkpoint = byteArrayFromBuffer(new Buffer(this.onCheckpoint()))
      this.sendBuffer(checkpointMessage)
    }
    else if(message instanceof MsgAttachTo) {
      throw new Error("Not implemented!")
    }
    else if(message instanceof MsgRPC) {
      // console.log("**** method: " + message.methodId)
      let logEntry = byteArrayToBuffer(message.serializedArgs)
      this.onLogEntry(logEntry)
    }
    else {
      throw new Error("Unknown message type!")
    }
  }

  isReplaying() {
      return this.replaying
  }

  selfRPC(logEntry: AmbrosiaLogEntry) {
    this.trace("selfRPC invoked")
    let message = new MsgRPC()
    message.methodId = 0
    message.isOutgoing = true
    message.isSelfCall = true
    message.serializedArgs = byteArrayFromBuffer(logEntry)
    this.sendBuffer(message)
  }

  sendBuffer(message: Message) {
    let serialized = serializeMessage(message)

    serialized.iterate((x: Buffer) => {
      this.trace("About to send buffer:")
      console.log(x)

      this.sendSocket.write(x, () => {
        this.trace("Transmitted buffer with type: " + message.typ + " and size: " + x.byteLength)
      })
    })
  }

  trace(message: string) {
    console.log("AMBROSIA LOG: " + message)
  }
}

export default Ambrosia