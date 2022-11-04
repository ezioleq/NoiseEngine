use std::mem;

use ash::vk;

use crate::{rendering::fence::GraphicsFence, interop::prelude::{InteropResult, ResultError}};

use super::{device_pool::VulkanDevicePool, errors::universal::VulkanUniversalError};

pub struct VulkanFence<'devpool> {
    pool: &'devpool VulkanDevicePool,
    inner: vk::Fence
}

impl<'devpool> VulkanFence<'devpool>{
    pub fn new(pool: &'devpool VulkanDevicePool, inner: vk::Fence) -> Self {
        Self { pool, inner }
    }

    pub fn inner(&self) -> vk::Fence {
        self.inner
    }

    /// # Safety
    /// All fences must be from the same device.
    unsafe fn wait(&self, fences: &[vk::Fence], wait_all: bool, timeout: u64) -> Result<bool, VulkanUniversalError> {
        match self.pool.vulkan_device().wait_for_fences(fences, wait_all, timeout) {
            Ok(()) => Ok(true),
            Err(err) => match err {
                vk::Result::TIMEOUT => Ok(false),
                _ => Err(err.into())
            }
        }
    }
}

impl Drop for VulkanFence<'_> {
    fn drop(&mut self) {
        unsafe {
            self.pool.vulkan_device().destroy_fence(self.inner, None);
        }
    }
}

impl GraphicsFence for VulkanFence<'_> {
    fn wait(&self, timeout: u64) -> InteropResult<bool> {
        match unsafe {
            self.wait(&[self.inner], false, timeout)
        } {
            Ok(is_signaled) => InteropResult::with_ok(is_signaled),
            Err(err) => InteropResult::with_err(err.into()),
        }
    }

    fn is_signaled(&self) -> InteropResult<bool> {
        match unsafe {
            self.pool.vulkan_device().get_fence_status(self.inner)
        } {
            Ok(i) => InteropResult::with_ok(i),
            Err(err) => return InteropResult::with_err(err.into()),
        }
    }

    unsafe fn wait_multiple(
        &self, fences: &[&&dyn GraphicsFence], wait_all: bool, timeout: u64
    ) -> Result<bool, ResultError> {
        let f: &[&&VulkanFence] = mem::transmute(fences);
        let mut vec = Vec::with_capacity(f.len());

        for fence in f {
            vec.push(fence.inner);
        }

        Ok(self.wait(&vec, wait_all, timeout)?)
    }
}