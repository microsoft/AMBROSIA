type uint8 = number; /* uint8 */

type uint32 = number; /* uint32 */

let maxUint32 = 4294967295

class uint64 { /* uint64 */
  high: uint32;
  low: uint32;
  constructor(high: uint32, low: uint32) {
    this.high = high;
    this.low = low;
  }
  add(y: uint64): void {
    let low = this.low + y.low;
    let high = this.high + y.high + Math.trunc(low / (maxUint32 + 1))
    low &= maxUint32;
    this.low = low;
    this.high = high;
  }
  lt(y: uint64): boolean {
    return (this.high < y.high || (this.high == y.high && this.low < y.low));    
  }
  sub(y: uint64) : void {
    if (this.lt(y)) {
      throw new Error("cannot subtract greater integer");
    }
    let low = this.low - y.low;
    let high = this.high - y.high;
    if (low < 0) {
      low - 1;
      high += maxUint32;
    }
    this.low = low;
    this.high = high;
  }
}

function add64(x: uint64, y: uint64): uint64 {
  let res = new uint64(x.high, x.low);
  res.add(y);
  return res;
}

function sub64(x: uint64, y: uint64): uint64 {
  let res = new uint64(x.high, x.low);
  res.sub(y);
  return res;
}

interface ByteArrayIndex { /* 2D coordinates, MUST NOT be considered as a uint64 because not all subarrays of a byte array are full */
  first: uint32;
  second: uint32;
}

function copyIndex(x: ByteArrayIndex) : ByteArrayIndex {
  return {first: x.first, second: x.second};
}

let hexits = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f']

class ByteArray {
  public bytes: uint8[][];
  constructor(x: uint8[]) {
    this.bytes = [x];
  }
  byteAt (index: ByteArrayIndex) : uint8 {
    let tindex : ByteArrayIndex = copyIndex(index);
    while (tindex.first < this.bytes.length && tindex.second == this.bytes[tindex.first].length) {
      tindex.first++;
      tindex.second = 0;
    }
    if (tindex.first >= this.bytes.length) {
       throw new Error("byteAt: high out of bounds");
    }
    if (tindex.second >= this.bytes[tindex.first].length) {
      throw new Error("byteAt: low out of bounds");
    }
    return this.bytes[tindex.first][tindex.second];
  }
  length () : uint64 {
    let res : uint64 = new uint64(0, 0);
    let i : uint32 = 0;
    for (; i < this.bytes.length; ++i) {
      // console.log(new uint64(0, this.bytes[i].length));
      res.add(new uint64(0, this.bytes[i].length));
    }
    // console.log("!!")
    // console.log(res)
    return res;
  }
  concat (y: ByteArray) : void {
    if (this.bytes.length > 0 && y.bytes.length == 1 && this.bytes[this.bytes.length - 1].length + y.bytes[0].length <= maxUint32) {
      this.bytes[this.bytes.length - 1] = this.bytes[this.bytes.length - 1].concat(y.bytes[0]);
    } else {
      /* TODO: pack this.bytes and y.bytes first? */
      this.bytes = this.bytes.concat(y.bytes);
    }
  }
  validIndex (x: ByteArrayIndex) : boolean {
    return (x.first < this.bytes.length && x.second < this.bytes[x.first].length);
  }
  dist (x: ByteArrayIndex, y: ByteArrayIndex) : uint64 {
    // console.log("starting slicelength")
    /* assume x <= y */
    let temp : ByteArrayIndex = {first: x.first, second: x.second};
    let res : uint64 = new uint64(0, 0);
    while (temp.first <= y.first) {
      // console.log("going from " + x.first + " " + x.second + " to " + y.first + " " + y.second)

      if(x.first == y.first) {
          let diff = y.second - x.second
          // console.log("adding diff 0 : " + diff)
          res.add(new uint64(0, diff))
      }
      else if(temp.first == x.first) {
        if (temp.second <= this.bytes[temp.first].length) {
          let diff = this.bytes[temp.first].length - x.second
          // console.log("length " + this.bytes[temp.first].length)
          // console.log("adding diff 1 : " + diff)
          res.add(new uint64(0, diff))
        }
      } 
      else if(temp.first < y.first) {
        let diff = this.bytes[temp.first].length
        // console.log("adding diff 2: " + diff)
        res.add(new uint64(0, diff))
      }
      else if(temp.first == y.first) {
        if (temp.second <= this.bytes[temp.first].length) {
          let diff = this.bytes[temp.first].length - y.second
          // console.log("adding diff 3: " + diff)
          res.add(new uint64(0, diff))
        }
      }
      else {
        throw new Error("slice length failure, unknown case")
      }

      temp.first++;
      temp.second = 0;
    }
    // console.log("slice length result: " + res.low)
    return res;
  }
  shiftIndex (x: ByteArrayIndex, y: uint32) {
    let tx : ByteArrayIndex = {first: x.first, second: x.second};
    let ty : uint32 = y;
    while (ty > 0 && tx.first < this.bytes.length) {
      if (tx.second <= this.bytes[tx.first].length) {
        if (ty > this.bytes[tx.first].length - tx.second) {
	  tx.first++;
	  ty -= this.bytes[tx.first].length - tx.second;
	  tx.second = 0;
	} else {
	  tx.second += ty;
	  ty = 0;
	}
      }
    }
    return tx;
  }
  shiftIndex64 (x: ByteArrayIndex, y: uint64) {
    let tx : ByteArrayIndex = {first: x.first, second: x.second};
    let ty : uint64 = new uint64(y.high, y.low);
    let tzero : uint64 = new uint64(0, 0);
    while (tzero.lt(ty) && tx.first < this.bytes.length) {
      if (tx.second <= this.bytes[tx.first].length) {
      	let len = new uint64(0, this.bytes[tx.first].length - tx.second);
        if (len.lt(ty)) {
	  tx.first++;
	  ty.sub(len);
	  tx.second = 0;
	} else {
	  tx.second += ty.low; /* here ty.high == 0 */
	  ty = tzero;
	}
      }
    }
    return tx;
  }
  subarray(from: ByteArrayIndex, to: ByteArrayIndex) {
    let res = new ByteArray([]);
    let tfrom = {first: from.first, second: from.second};
    let tto = {first: to.first, second: to.second};
    while (tfrom.first <= tto.first) {
      if (tfrom.first == tto.first && tfrom.second <= tto.second && (tfrom.second !== 0 || tto.second < this.bytes[tfrom.first].length)) {
        // console.log("concat for res")
        res.concat(new ByteArray(this.bytes[tfrom.first].slice(tfrom.second, tto.second)));
        // console.log(res.toHexString())
      }
      else if (tfrom.second !== 0) {
        if (this.bytes[tfrom.first].length < tfrom.second) {
          throw new Error("Bad tfrom index");
        }
        res.concat(new ByteArray(this.bytes[tfrom.first].slice(tfrom.second)));
      } else {
        res.concat(new ByteArray(this.bytes[tfrom.first]));
      }
      tfrom.first++;
      tfrom.second = 0;
    }
    // console.log("subarray returning ")
    // console.log(res.toHexString())
    return res;
  }
  endIndex() {
    if (this.bytes.length > 0) {
        return {first: this.bytes.length - 1, second: this.bytes[this.bytes.length - 1].length};
    } else {
        return {first: 0, second: 0};
    }
  }
  toHexString() {
      let res = "";
      let i = 0;
      for (; i < this.bytes.length; ++i) {
          let b = this.bytes[i];
          let j = 0;
          for (; j < b.length; ++j) {
              let v = b[j];
              res = res.concat(hexits[Math.trunc(v/16)]);
              res = res.concat(hexits[v%16]);
              res = res.concat(" ");
          }
      }
      return res;
  }

  iterate(f: (x: Buffer) => void) {
      let i = 0;
      for (; i < this.bytes.length; ++i) {
        let buf = Buffer.from(this.bytes[i])
        f(buf)
      }
  }
}

function byteArrayFromBuffer(b: Buffer) : ByteArray {
  // console.log("** serializing")
  // console.log(b)
  let res = new ByteArray([])
  for (var byte of b.values()) {
    res.concat(new ByteArray([byte]))
  }
  return res
}

function byteArrayToBuffer(byteArray: ByteArray) : Buffer {
  // console.log("** byte array")
  // console.log(byteArray.toHexString())
  let buffers : Buffer[] = []
  let i = 0
  for (; i < byteArray.bytes.length; ++i) {
    buffers.push(new Buffer(byteArray.bytes[i]))
  }
  let concatd = Buffer.concat(buffers)
  // console.log("** about to deserialize")
  // console.log(concatd)
  return concatd
}

interface slice {
  bytes: ByteArray;
  from: ByteArrayIndex;
  to: ByteArrayIndex;
}

function sliceLength(input: slice) : uint64 {
  return input.bytes.dist(input.from, input.to);
}

function checkEnd(input: slice) {
  if ((new uint64(0, 0)).lt(sliceLength(input))) {
    throw new Error("Data present, none expected, slice length: " + sliceLength(input).low);
  }
}

function sliceCrop(input: slice) : ByteArray {
  return input.bytes.subarray(input.from, input.to);
}

function wholeSlice(b: ByteArray) : slice {
    return {
        bytes: b,
        from: {first: 0, second: 0},
        to: b.endIndex()
    }
}

function unsafeParseByte(input: slice) : uint8 {
  let res = input.bytes.byteAt(input.from);
  input.from = input.bytes.shiftIndex(input.from, 1);
  return res;
}

function parseByte(input: slice) : uint8 {
  if (sliceLength(input).lt(new uint64(0, 1))) {
    throw new Error("parseIntFixed: not enough bytes");
  }
  return unsafeParseByte(input);
}

function serializeByte(x: uint8) : ByteArray {
    return new ByteArray([x]);
}

function parseByteArray(input: slice, size: uint64) : ByteArray {
    if (sliceLength(input).lt(size)) {
        throw new Error("parseByteArray: not enough bytes");
    }
    let newFrom = input.bytes.shiftIndex64(input.from, size);
    let res = input.bytes.subarray(input.from, newFrom);
    input.from = newFrom;
    return res;
}

function serializeByteArray(array: ByteArray) : ByteArray {
    let res = new ByteArray([]);
    /* TODO: we should do a deep copy, not just a shallow copy */
    res.concat(array);
    return res;
}

function parseIntFixed(input: slice) : uint32 /* intFixed */ {
  if (sliceLength(input).lt(new uint64(0, 4))) {
    throw new Error("parseIntFixed: not enough bytes");
  }
  let b0 = unsafeParseByte(input);
  let b1 = unsafeParseByte(input);
  let b2 = unsafeParseByte(input);
  let b3 = unsafeParseByte(input);
  /* little-endian */
  return (b0 + 256 * (b1 + 256 * (b2 + 256 * b3)));
}

function serializeIntFixed(x: uint32): ByteArray {
  let t = x;
  let b0 = t % 256;
  t = Math.trunc(t / 256);
  let b1 = t % 256;
  t = Math.trunc(t / 256);
  let b2 = t % 256;
  t = Math.trunc(t / 256);
  let b3 = t % 256;
  /* little-endian */
  return new ByteArray([b0, b1, b2, b3]);
}

function parseLongFixed(input: slice) : uint64 {
  /* little-endian */
  let low = parseIntFixed(input);
  let high = parseIntFixed(input);
  return new uint64(high, low);
}

function serializeLongFixed(x: uint64 /* longFixed */): ByteArray {
  /* little-endian */
  let res : ByteArray = serializeIntFixed(x.low);
  res.concat(serializeIntFixed(x.high));
  return res;
}

type zigzagInt = uint32; // uint64; // /* unsigned variable-length int,  */


function zigzagIntSize(value : uint32) : uint32 {
   var sz : uint32 = 0;
   var zigZagEncoded : uint32 = maxUint32 & ((value << 1) ^ (value >> 31));
   while ((zigZagEncoded & ~0x7F) !== 0) {
      sz++;
      zigZagEncoded >>= 7;
   }
   return sz + 1;
}

function zigzagInt64Size(value : uint64) : uint32 {
  // The size shall never use the high bits
  return serializeZigzagInt64(value).length().low;
}

function parseZigzagInt(input: slice) : zigzagInt {
  var shift : uint32 = 7;
  var currentByte : uint8 = parseByte(input);
  var read : uint8 = 1;
  var result : uint32 = currentByte & 0x7F;
  while ((currentByte & 0x80) !== 0) {
      currentByte = parseByte(input);
      read++;
      result |= (currentByte & 0x7F) << shift;
      shift += 7;
      if (read > 5) {
         throw new Error("parseZigzagInt: number is too long");
      }
  }
  result = ((-(result & 1) ^ ((result >> 1) & 0x7FFFFFFF)));
  result &= maxUint32;
  return result;
}

function parseZigzagInt64(input: slice) : uint64 {
  var shift : uint32 = 7;
  var currentByte : uint8 = parseByte(input);
  var read : uint8 = 1;
  var hi : uint32 = 0;
  var lo : uint32 = currentByte & 0x7F;
  while ((currentByte & 0x80) !== 0) {
      currentByte = parseByte(input);
      read++;
      if (shift + 7 <= 32) {
        lo |= (currentByte & 0x7F) << shift;
      } else if (shift < 32) {
        var currentBytel = currentByte & ((1 << (32 - shift)) - 1);
        var currentByteh = currentByte >> (32 - shift);
        lo |= (currentBytel & 0x7F) << shift;
        hi |= (currentByteh & 0x7F) << 0;
      } else {
        hi |= (currentByte & 0x7F) << (shift - 32);
      }
      shift += 7;
      if (read > 9) {
         throw new Error("parseZigzagInt: number is too long");
      }
  }
  var hilsb : uint32 = hi & 1;
  var lolsb : uint32 = lo & 1;
  lo = ((lo >> 1) | (hilsb << 31)) ^ (-lolsb); 
  hi = (hi >> 1) ^ (-lolsb);
  return new uint64(hi, lo);
}

function serializeZigzagInt(value: zigzagInt): ByteArray {
  var zigZagEncoded : uint32 = maxUint32 & ((value << 1) ^ (value >> 31));
  var bytes : uint8[] = [];
  while ((zigZagEncoded & ~0x7F) !== 0) {
    bytes = bytes.concat (0xFF & (zigZagEncoded | 0x80)); 
    zigZagEncoded >>= 7;
  }
  bytes = bytes.concat(0xFF & zigZagEncoded);

  return new ByteArray(bytes);
}

function serializeZigzagInt64(value: uint64): ByteArray {
  var hi : uint32 = value.high;
  var lo : uint32 = value.low;
  var himsb = hi >> 31;
  var lomsb = lo >> 31;
  var nhi = (hi << 1) ^ lomsb;
  var nlo = (lo << 1) ^ himsb;
  var bytes : uint8[] = [];
  while (nhi !== 0 || ((nlo & ~0x7F) !== 0)) {
    bytes = bytes.concat (0xFF & (nlo | 0x80));
    var hilsb7 = nhi & 0x7F;
    nhi >>= 7;
    nlo = (nlo >>= 7) ^ (hilsb7 << (32 - 7));
  }
  bytes = bytes.concat(0xFF & nlo);

  return new ByteArray(bytes);
}

interface header {
  committerID: uint32 /* intFixed */;
  size: uint32 /* intFixed */;
  check: uint64 /* longFixed */;
  logRecordSequenceID: uint64 /* longFixed */;
}

function parseHeader(input: slice) : header {
  let committerID = parseIntFixed(input);
  let size = parseIntFixed(input);
  let check = parseLongFixed(input);
  let logRecordSequenceID = parseLongFixed(input);
  return {
    committerID: committerID,
    size: size,
    check: check,
    logRecordSequenceID: logRecordSequenceID
  };
}

function serializeHeader(x: header): ByteArray {
  let res : ByteArray = serializeIntFixed(x.committerID);
  res.concat(serializeIntFixed(x.size));
  res.concat(serializeLongFixed(x.check));
  res.concat(serializeLongFixed(x.logRecordSequenceID));
  return res;
}

enum MessageType {
  TrimTo = 14,
  CountReplayableRPCBatch = 13,
  UpgradeService = 12,
  TakeBecomingPrimaryCheckpoint = 11,
  UpgradeTakeCheckpoint = 10,
  InitialMessage = 9,
  Checkpoint = 8,
  RPCBatch = 5,
  TakeCheckpoint = 2,
  AttachTo = 1,
  RPC = 0
}

function parseMessageType (input: slice) : MessageType {
  let v : uint8 = input.bytes.byteAt(input.from);
  input.from = input.bytes.shiftIndex(input.from, 1);
  if (MessageType[v]) {
    let res : MessageType = v;
    return res;
  }
  throw new Error("parseMessageType: invalid messageType");
}

function serializeMessageType (x: MessageType) : ByteArray {
  return new ByteArray([x]);
}

class Message {
  typ: MessageType;
  protected constructor(typ: MessageType) {
    this.typ = typ;
  }
  serializePayload() : ByteArray {
    throw new Error("Unimplemented: message.serializePayload");
  }
}

class EmptyMessage extends Message {
    constructor(typ: MessageType) {
        super(typ);
    }
    serializePayload() : ByteArray {
        return new ByteArray([]);
    }
}

class MsgUpgradeService extends EmptyMessage {
  constructor() {
    super(MessageType.UpgradeService);
  }
}

class MsgTakeBecomingPrimaryCheckpoint extends EmptyMessage {
  constructor() {
    super(MessageType.TakeBecomingPrimaryCheckpoint);
  }
}

class MsgUpgradeTakeCheckpoint extends EmptyMessage {
  constructor() {
    super(MessageType.UpgradeTakeCheckpoint);
  }
}

/* MsgInitialMessage depends on MsgRPC */

class MsgCheckpoint extends Message {
  checkpoint: ByteArray;
  expectedCheckpointSize: uint64; /* used only for parsing */
  /* constructor: only parse the payload, i.e. fills in
     expectedCheckpointSize; then checkpoint must be filled in
     later by the caller, not the constructor */
  constructor(input?: slice) {
    super(MessageType.Checkpoint);
    if (input) {
        let sz = parseZigzagInt64(input);
        this.expectedCheckpointSize = sz;
        this.checkpoint = new ByteArray([]);
    } else {
      this.expectedCheckpointSize = new uint64(0, 0);
      this.checkpoint = new ByteArray([]);
    }
  }
  /* serializePayload should only serialize the payload excluding the actual checkpoint contents */
  serializePayload () : ByteArray {
      return serializeZigzagInt64(this.checkpoint.length());
  }
}

/* RPCBatch depends on RPC */

class MsgTakeCheckpoint extends EmptyMessage {
  constructor() {
    super(MessageType.TakeCheckpoint);
  }
}

class MsgAttachTo extends Message {
    destinationBytes: ByteArray;
    constructor(input?: slice) {
        super(MessageType.AttachTo);
        if (input) {
            this.destinationBytes = sliceCrop(input);
            input.from = copyIndex(input.to);
        } else {
            this.destinationBytes = new ByteArray([]);
        }
    }
    serializePayload () : ByteArray {
        let res : ByteArray = new ByteArray([]);
        res.concat(this.destinationBytes);
        return res;
    }
}

class MsgRPC extends Message {
    destinationServiceName: Buffer;
    methodId: uint32 /* zigzagInt */ ;
    serializedArgs: ByteArray;
    isOutgoing: boolean;
    isSelfCall: boolean;
    constructor(input?: slice) {
        super(MessageType.RPC);
        // console.log("we are here in msg rpc")
        /* parsing ignores the destination service name */
        this.destinationServiceName = new Buffer([])
        if (input) {
            this.isOutgoing = false;
            this.isSelfCall = false;
            // Omitted in the incoming RPC.
            let reservedRPCOrReturn = parseByte(input); // value should be 0, but do we care at parsing?
            this.methodId = parseZigzagInt(input);
            let reservedFireAndForgetOrAsyncAwait = parseByte(input); // value should be 1, but do we care at parsing?
            this.serializedArgs = sliceCrop(input);
            input.from = copyIndex(input.to);
        } else {
            this.isOutgoing = true;
            this.isSelfCall = false;
            this.methodId = 0;
            this.serializedArgs = new ByteArray([]);
        }
    }
    serializePayload () : ByteArray {
        let res : ByteArray = new ByteArray([]);
        if (this.isOutgoing) {
          /* serialization needs destination service name */
          if(this.isSelfCall) {
            res.concat(serializeZigzagInt(0));
          } else {
            res.concat(serializeZigzagInt(this.destinationServiceName.length));
            res.concat(byteArrayFromBuffer(this.destinationServiceName));
          }
        }
        res.concat(serializeByte(0)); /* reserved: RPC or Return */
        res.concat(serializeZigzagInt(this.methodId));
        res.concat(serializeByte(2)); /* reserved: Fire and Forget (1) or Async/Await (0) or Impulse (2) */
        res.concat(this.serializedArgs);
        return res;
    }
}

function serializeMessage (msg: Message) : ByteArray {
  let payload : ByteArray = msg.serializePayload ();
  let typ : ByteArray = serializeMessageType(msg.typ);
  let sz = add64(payload.length(), typ.length());
  // console.log("payload: ")
  // console.log(payload)
  // console.log("typ: ")
  // console.log(typ)
  // console.log("sz: ")
  // console.log(sz)
  if (sz.high > 0) {
    throw new Error("message is too large, its size should fit in 32 bits");
  }
  let res : ByteArray = serializeZigzagInt(sz.low);
  res.concat(typ);
  res.concat(payload);
  /* Special case: concat the checkpoint */
  if (msg.typ == MessageType.Checkpoint) {
    res.concat((msg as MsgCheckpoint).checkpoint);
  }
  return res;
}

class MsgCountReplayableRPCBatch extends Message {
    batch: MsgRPC[];
    replayable: uint32;
    constructor(input?: slice, parseMessage?: ((input: slice) => MsgRPC)) {
        super(MessageType.RPCBatch);
        if (input) {
            if (parseMessage) {
                let res : MsgRPC[] = [];
                let count = parseZigzagInt(input);
                this.replayable = parseZigzagInt(input);
                let i = 0;
                for (; i < count; ++i) {
                    res = res.concat([parseMessage(input)]);
                }
                this.batch = res;
            } else {
                throw new Error("MsgRPCBatch: requires parseMessage");
            }
        } else {
            this.batch = [];
            this.replayable = 0;
        }
    }
    serializePayload() : ByteArray {
        let res : ByteArray = serializeZigzagInt(this.batch.length);
        res.concat(serializeZigzagInt(this.replayable));
        let i = 0;
        for (; i < this.batch.length; ++i) {
            res.concat(serializeMessage(this.batch[i]));
        }
        return res;
    }
}

class MsgInitialMessage extends Message {
    rpc: MsgRPC;
    constructor(input?: slice, parseMessage?: ((input: slice) => MsgRPC)) {
        super(MessageType.InitialMessage);
        if (input) {
            if (parseMessage) {
                this.rpc = parseMessage(input);
            } else {
                throw new Error("MsgInitialMessage: message parser required");
            }
        } else {
            this.rpc = new MsgRPC();
        }
    }
    serializePayload() : ByteArray {
        if (this.rpc.isOutgoing) {
           throw new Error("Initial message RPC should be an Incoming one");
        }
        return serializeMessage(this.rpc);
    }
}

class MsgRPCBatch extends Message {
    batch: MsgRPC[];
    constructor(input?: slice, parseMessage?: ((input: slice) => MsgRPC)) {
        super(MessageType.RPCBatch);
        if (input) {
            if (parseMessage) {
                let res : MsgRPC[] = [];
                let count = parseZigzagInt(input);
                let i = 0;
                for (; i < count; ++i) {
                    res = res.concat([parseMessage(input)]);
                }
                this.batch = res;
            } else {
                throw new Error("MsgRPCBatch: requires parseMessage");
            }
        } else {
            this.batch = [];
        }
    }
    serializePayload() : ByteArray {
        let res : ByteArray = serializeZigzagInt(this.batch.length);
        let i = 0;
        for (; i < this.batch.length; ++i) {
            res.concat(serializeMessage(this.batch[i]));
        }
        return res;
    }
}

function parseMessage (input: slice, expectedTyp?: MessageType) : Message {
  function parseRPCMessage (input: slice) : MsgRPC {
      let res = parseMessage(input, MessageType.RPC);
      return res as MsgRPC;
  }
  let totalSize : uint64 = new uint64(0, parseZigzagInt(input));
  // console.log("totalSize: " + totalSize.low)
  if (sliceLength(input).lt(totalSize)) {
    throw new Error("parseMessage: not enough bytes for payload");
  }
  let innerTo = input.bytes.shiftIndex64(input.from, totalSize);
  let inner : slice = {
      bytes: input.bytes,
      from: copyIndex(input.from),
      to: innerTo
  };
  let typ : MessageType = parseMessageType(inner);
  // console.log("type: " + typ)
  if (expectedTyp) {
      if (typ !== expectedTyp) {
          throw new Error("Found a message of a different type than the one expected");
      }
  }
  var res : Message;
  switch (typ) {
    case MessageType.CountReplayableRPCBatch:
      res = new MsgCountReplayableRPCBatch(inner, parseRPCMessage);
      break;
    case MessageType.UpgradeService:
      res = new MsgUpgradeService();
      break;
    case MessageType.TakeBecomingPrimaryCheckpoint:
      res = new MsgTakeBecomingPrimaryCheckpoint();
      break;
    case MessageType.UpgradeTakeCheckpoint:
      res = new MsgUpgradeTakeCheckpoint();
      break;
    case MessageType.InitialMessage:
      res = new MsgInitialMessage(inner, parseRPCMessage);
      // console.log("after new initial message")
      break;
    case MessageType.Checkpoint:
      res = new MsgCheckpoint(inner);
      break;
    case MessageType.RPCBatch:
      res = new MsgRPCBatch(inner, parseRPCMessage);
      break;
    case MessageType.TakeCheckpoint:
      res = new MsgTakeCheckpoint();
      break;
    case MessageType.AttachTo:
      res = new MsgAttachTo(inner);
      break;
    case MessageType.RPC:
      res = new MsgRPC(inner);
      // console.log("after new RPC message")
      break;
    default:
      throw new Error("Unsupported message type");
  }

  /* Check that we consumed every byte of the payload */
  checkEnd(inner);

  /* Notify the input that we consumed the payload bytes */
  input.from = innerTo;

  return res;
}

interface logRecord {
  header: header;
  msgs: Message[];
}

let headerSize = 24;
let headerSize64 = new uint64(0, headerSize);

function parseLogRecords (input: slice) : logRecord[] {
  console.log("LOG RECORDS input size " + input.bytes.length().low)

  let logRecords : logRecord[] = [];
  let zero = new uint64(0, 0);
  while (zero.lt(sliceLength(input))) {
    // console.log("slice length is now: " + sliceLength(input).low)

    /* Figure out how big the next log record is */
    let tmpSlice: slice = {
      bytes: input.bytes,
      from: copyIndex(input.from),
      to: copyIndex(input.to)
    };

    let header = parseHeader(tmpSlice);
    // console.log("slice size is " + header.size)

    /* Create a slice for that log record */
    let logRecord = parseLogRecord(input)
    logRecords.push(logRecord)
  }

  return logRecords
}

function parseLogRecord (input: slice) : logRecord {
  // console.log("LOG RECORD input size " + input.bytes.length().low)

  let header = parseHeader(input);
  // console.log("headr size " + header.size)
  if (header.size < headerSize) {
    throw new Error("Size must include header size");
  }
  let msgsSize = header.size - headerSize;
  let msgsSize64 = new uint64(0, msgsSize);
  // console.log("msgs size " + msgsSize64.low)

  if (sliceLength(input).lt(msgsSize64)) {
    throw new Error("Not enough message bytes remaining");
  }
  let innerTo = input.bytes.shiftIndex(input.from, msgsSize);
  // console.log("input.from: " + input.from.second)
  // console.log("innerTo: " + innerTo.second)
  let inner: slice = {
    bytes: input.bytes,
    from: copyIndex(input.from),
    to: innerTo
  };
  let mySliceLength = sliceLength(inner).low
  // console.log("slice length: " + mySliceLength)
  let msgs : Message[] = [];
  let zero = new uint64(0, 0);
  let maybeCheckpointEndPosition : ByteArrayIndex = innerTo;

  while (zero.lt(sliceLength(inner))) {
    // console.log("parsing message")
    let msg = parseMessage(inner)
    msgs = msgs.concat([msg]);

    if(msg instanceof MsgCheckpoint) {
      /* Use slice to grab the checkpoint off the stream if we just saw a checkpoint message */
      let expectedCheckpointSize = (msg as MsgCheckpoint).expectedCheckpointSize
      let innerCheckpointTo = input.bytes.shiftIndex64(innerTo, expectedCheckpointSize)
      let innerCheckpoint: slice = {
        bytes: input.bytes,
        from: copyIndex(innerTo),
        to: innerCheckpointTo
      };

      // console.log("checkpoint message!")

      /* Verify we have enough bytes to process the checkpoint, else wait */
      if (sliceLength(innerCheckpoint).lt(expectedCheckpointSize)) {
        console.log("sliceLength: " + sliceLength(innerCheckpoint).low)
        throw new Error("parseMessage: not enough bytes for checkpoint yet");
      }

      /* Take the checkpoint off of the stream */
      (msg as MsgCheckpoint).checkpoint = parseByteArray(innerCheckpoint, (msg as MsgCheckpoint).expectedCheckpointSize);
      maybeCheckpointEndPosition = innerCheckpointTo

      // console.log("done capturing checkpoint")
    }

    // console.log("done parsing message")
  }

  input.from = maybeCheckpointEndPosition;

  return {
    header: header,
    msgs: msgs
  }
}

/* this serializer also adjusts the size field of the header */

function serializeLogRecord(header: header, msgs: Message[]) {
  var i: number;
  let n = msgs.length;
  let lowMsgs : ByteArray = new ByteArray([]);
  for (i = 0; i < n; ++i) {
    lowMsgs.concat(serializeMessage(msgs[i]));
  }
  let size = add64(new uint64(0, headerSize), lowMsgs.length());
  if (size.high > 0) {
    throw new Error("log record too long");
  }
  header.size = size.low;
  let res : ByteArray = serializeHeader(header);
  res.concat(lowMsgs);
  return res;
}

export { serializeMessage, serializeLogRecord, parseLogRecord, ByteArray, wholeSlice }
export { Message, MessageType }
export { MsgCountReplayableRPCBatch, MsgUpgradeService, MsgTakeBecomingPrimaryCheckpoint, MsgUpgradeTakeCheckpoint }
export { MsgInitialMessage, MsgCheckpoint, MsgRPCBatch, MsgTakeCheckpoint, MsgAttachTo, MsgRPC }
export { uint64, uint32 }
export { byteArrayToBuffer, byteArrayFromBuffer, parseLogRecords }