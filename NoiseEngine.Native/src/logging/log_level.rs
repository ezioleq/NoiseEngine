#[repr(u8)]
#[derive(Clone, Copy)]
pub enum LogLevel {
    Debug = 1 << 0,
    Trace = 1 << 1,
    Info = 1 << 2,
    Warning = 1 << 3,
    Error = 1 << 4,
    Fatal = 1 << 5,
}
