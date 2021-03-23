    /**
     * Test for a literal-object array type; this should generate a 'NickNames_Element' class and then redefine the type of NickNames as Nicknames_Element[].
     * This is done to makes it easier for the consumer to create a NickNames instance.
     * @ambrosia publish = true 
     */
    export type NickNames = { name: string }[];




