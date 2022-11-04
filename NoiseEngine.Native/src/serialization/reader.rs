use std::{ptr, mem};

pub struct SerializationReader<'a> {
    pub index: usize,
    pub data: &'a [u8]
}

impl<'a> SerializationReader<'a> {
    pub fn new(data: &'a [u8]) -> SerializationReader {
        SerializationReader { index: 0, data }
    }

    pub fn read<T>(&mut self) -> Option<T> {
        let size = mem::size_of::<T>();
        if self.index + size > self.data.len() {
            return None
        }

        let mut ptr = self.data.as_ptr() as *const u8;
        ptr = unsafe {
            ptr.offset(self.index as isize)
        };

        self.index += size;

        Some(unsafe {
            ptr::read_unaligned::<T>(ptr as *const T)
        })
    }

    pub fn read_unchecked<T>(&mut self) -> T {
        let mut ptr = self.data.as_ptr() as *const u8;
        ptr = unsafe {
            ptr.offset(self.index as isize)
        };

        self.index += mem::size_of::<T>();

        unsafe {
            ptr::read_unaligned::<T>(ptr as *const T)
        }
    }
}