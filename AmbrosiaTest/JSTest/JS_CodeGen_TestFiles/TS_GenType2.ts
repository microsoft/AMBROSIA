/**
 * Generic built-in types can be used, but only with concrete types (not type placeholders, eg. "T"): Example #2
 * @ambrosia publish = true 
 */
export type EmployeeWithGenerics = { firstNames: Set<{ name: string, nickNames: NickNames }>, lastName: string, birthYear: number };

/** 
 * Test for a literal-object array type; this should generate a 'NickNames_Element' class and then redefine the type of NickNames as Nicknames_Element[].
 * This is done to makes it easier for the consumer to create a NickNames instance.
 * @ambrosia publish = true 
 */
export type NickNames = { name: string }[];



