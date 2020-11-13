class Employee implements ISerializationFilterable
{
    public _nonSerializablePropertyList: string[]; // Required for ISerializationFilterable
    public _companyName: string;

    constructor(companyName: string = "Microsoft")
    {
        this._companyName = companyName;
        this._nonSerializablePropertyList = ["_companyName"];
    }
}

class User extends Employee implements ISerializationFilterable
{
    public _nonSerializablePropertyList: string[]; // Required for ISerializationFilterable
    public _firstName: string;
    public _lastName: string;

    constructor(first: string, last: string)
    {
        super();
        this._firstName = first;
        this._lastName = last;
        // Note: If the parent class also implements ISerializationFilterable then this.nonSerializablePropertyList will already be set
        this._nonSerializablePropertyList = (this._nonSerializablePropertyList || []).concat(["_lastName"]);
    }
}

interface ISerializationFilterable
{
    _nonSerializablePropertyList: string[];
}

class JsonSerializer
{
    /** Type-guard for ISerializationFilterable. */
    static isSerializationFilterable<T>(o: any): o is ISerializationFilterable
    {
        if (o && (typeof o === "object"))
        {
            return (o["_nonSerializablePropertyList"] instanceof Array);
        }

        return (false);
    }

    static serialize<T>(o: T): string
    {
        let serialized: string = JSON.stringify(user, (key: string, value: any) =>
        {
            // The first call to replacer() always passes an empty key ("")
            if (!key)
            {
                // Note: Returning 'undefined' here would cause the whole serialization to halt
                return (value);
            }
            if (JsonSerializer.isSerializationFilterable(o) && (o._nonSerializablePropertyList.indexOf(key) !== -1))
            {
                return (undefined);
            }
            return (value);
        });

        return (serialized);
    }

    static deserialize<T>(json: string, ctor: { new(...args: any[]): T }, ...ctorArgs: any[]): T
    {
        let o: object = JSON.parse(json);
        let newInstance: T = new ctor(...ctorArgs);

        for (const key of Object.keys(o))
        {
            if (JsonSerializer.isSerializationFilterable(newInstance) && (newInstance._nonSerializablePropertyList.indexOf(key) !== -1))
            {
                continue;
            }
            newInstance[key] = o[key];
        }

        return (newInstance);
    }
}

let user: User = new User("John", "Doe");
user._companyName = "Apple";
let json: string = JsonSerializer.serialize(user);
let newUser: User = JsonSerializer.deserialize(json, User);
console.log(newUser);
