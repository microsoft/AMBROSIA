export module Test
{
    /** 
     * Method with single 'rawParams: Uint8Array' parameter cannot be a Post method (ie. missing the 'methodID=' attribute)
     * @param rawParams Description of the format of the custom serialized byte array.
     * @ambrosia publish=true
     */
    export function takesCustomSerializedParams(rawParams: Uint8Array): void {
    }


}

